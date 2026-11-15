using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniTest;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class LifecycleHubStateProjectTests
    {
        // Internal
        private static readonly string[] _fixedPath =
        {
            "Observe", "Direct0", "DuplicateClient0", "DuplicateDirect0", "Client1", "RemoveEvent1",
            "Client1", "RemoveClient0", "Client0", "Direct2", "Null3", "DuplicateDirect3", "RemoveEvent3",
            "Direct3", "Dispose", "CheckDisposed", "Restart", "Direct0", "Client1", "DisposeHandledFailure",
            "CheckDisposed", "Restart", "Client2", "Direct3", "DisposeReentrant", "CheckDisposed", "Restart",
            "DisposeUnhandledFailure", "CheckDisposed", "Restart", "Direct1", "Dispose", "CheckDisposed"
        };

        private class HubModel : Model
        {
            public LifecycleHub Hub => (LifecycleHub)Subject;
            public Action[] Keys { get; } = new Action[4];
            public Action[] ClientCallbacks { get; } = new Action[4];
            public int[] Registrations { get; } = new[] { -1, -1, -1, -1 };
            public int[] Calls { get; } = new int[8];
            public int[] ExpectedCalls { get; } = new int[8];
            public List<object> ExtraKeys { get; } = new();
            public ILifecycleHubBuilder SavedBuilder { get; set; }
            public bool Disposed { get; set; }

            public HubModel()
            {
                for (var index = 0; index < Keys.Length; index++)
                {
                    var slot = index;
                    Keys[index] = () => Calls[slot]++;
                    ClientCallbacks[index] = () => Calls[slot + Keys.Length]++;
                }
            }
        }

        private class HubProject : CompactProject<HubModel>, IDisposable
        {
            private readonly bool _continuous;
            private readonly ConcurrentDictionary<HubModel, byte> _active = new();

            public HubProject(bool continuous = false) : base(ExecutionMode.Inline) => _continuous = continuous;

            public override IEnumerable<CompactLab<HubModel>> CreateCompactLabs(HubModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return CheckedLab("Create", CreateHub);
                    yield break;
                }

                if (!_continuous && model.ExecutionCount > _fixedPath.Length) yield break;

                foreach (var lab in CreateOperations(model))
                {
                    if (_continuous || lab.Id == _fixedPath[model.ExecutionCount - 1]) yield return lab;
                }
            }

            private IEnumerable<CompactLab<HubModel>> CreateOperations(HubModel model)
            {
                if (model.Disposed)
                {
                    yield return CheckedLab("CheckDisposed", CheckDisposed);
                    yield return CheckedLab("Restart", CreateHub);
                    yield break;
                }

                yield return CheckedLab("Observe", state =>
                {
                    state.Hub.Disconnect(null);
                    state.Hub.Disconnect(new object());
                    state.Hub.OnDisposed += null;
                    state.Hub.OnDisposed -= null;
                });

                for (var index = 0; index < model.Keys.Length; index++)
                {
                    var slot = index;
                    if (model.Registrations[slot] == -1)
                    {
                        yield return CheckedLab($"Direct{slot}", state =>
                        {
                            state.Hub.OnDisposed += state.Keys[slot];
                            state.Registrations[slot] = slot;
                        });
                        yield return CheckedLab($"Client{slot}", state =>
                        {
                            state.Hub.Connect(state.Keys[slot]).OnDisposed(state.ClientCallbacks[slot]);
                            state.Registrations[slot] = slot + state.Keys.Length;
                        });
                        yield return CheckedLab($"Null{slot}", state =>
                        {
                            state.Hub.Connect(state.Keys[slot]).OnDisposed(null);
                            state.Registrations[slot] = -2;
                        });
                    }
                    else
                    {
                        yield return CheckedLab($"DuplicateDirect{slot}", state =>
                        {
                            Assert.Throws<InvalidOperationException>(() => state.Hub.OnDisposed += state.Keys[slot]);
                        });
                        yield return CheckedLab($"DuplicateClient{slot}", state =>
                        {
                            Assert.Throws<InvalidOperationException>(() =>
                                state.Hub.Connect(state.Keys[slot]).OnDisposed(state.ClientCallbacks[slot]));
                        });
                    }

                    yield return CheckedLab($"RemoveEvent{slot}", state =>
                    {
                        state.Hub.OnDisposed -= state.Keys[slot];
                        state.Registrations[slot] = -1;
                    });
                    yield return CheckedLab($"RemoveClient{slot}", state =>
                    {
                        state.Hub.Disconnect(state.Keys[slot]);
                        state.Registrations[slot] = -1;
                    });
                }

                yield return CheckedLab("Dispose", DisposeHub);
                yield return CheckedLab("DisposeHandledFailure", DisposeHandledFailure);
                yield return CheckedLab("DisposeReentrant", DisposeReentrant);

                if (model.Registrations.All(registration => registration == -1))
                    yield return CheckedLab("DisposeUnhandledFailure", DisposeUnhandledFailure);
            }

            private static CompactLab<HubModel> CheckedLab(string id, Action<HubModel> actor) =>
                new(id, actor: actor, asserter: AssertState);

            private void CreateHub(HubModel model)
            {
                if (model.Subject != null) CheckDisposed(model);
                model.Subject = new LifecycleHub();
                model.SavedBuilder = model.Hub.Connect(model.Keys[0]);
                model.Disposed = false;
                model.ExtraKeys.Clear();
                _active.TryAdd(model, 0);
            }

            private void DisposeHub(HubModel model)
            {
                foreach (var registration in model.Registrations)
                {
                    if (registration >= 0) model.ExpectedCalls[registration]++;
                }

                model.Disposed = true;
                Array.Fill(model.Registrations, -1);
                model.Hub.Dispose();
                _active.TryRemove(model, out _);
            }

            private void DisposeHandledFailure(HubModel model)
            {
                var failure = new ApplicationException("disposal callback");
                var reports = new List<Exception>();
                var badCalls = 0;
                var tailCalls = 0;
                Action bad = () => { badCalls++; throw failure; };
                var callback = bad + (() => tailCalls++);
                model.Hub.OnDisposed += callback;
                model.ExtraKeys.Add(callback);

                // Unexpected failures, including assertions, must not be swallowed by this policy.
                ExceptionHandler.SetHandler(exception =>
                {
                    if (!ReferenceEquals(exception, failure)) throw exception;
                    reports.Add(exception);
                });

                try
                {
                    DisposeHub(model);
                }
                finally
                {
                    ExceptionHandler.SetHandler(null);
                }

                Assert.That(reports, Is.EqualTo(new[] { failure }));
                Assert.That(badCalls, Is.EqualTo(1));
                Assert.That(tailCalls, Is.Zero);
            }

            private void DisposeUnhandledFailure(HubModel model)
            {
                var failure = new ApplicationException("unhandled disposal callback");
                var calls = 0;
                var tailCalls = 0;
                Action bad = () => { calls++; throw failure; };
                var key = new object();
                model.Hub.Connect(key).OnDisposed(bad + (() => tailCalls++));
                model.ExtraKeys.Add(key);
                Assert.That(Assert.Throws<ApplicationException>(model.Hub.Dispose), Is.SameAs(failure));
                model.Disposed = true;
                _active.TryRemove(model, out _);
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(tailCalls, Is.Zero);
            }

            private void DisposeReentrant(HubModel model)
            {
                var calls = 0;
                Action callback = () =>
                {
                    calls++;
                    Assert.That(model.Hub.IsDisposed, Is.True);
                    foreach (var key in model.Keys)
                    {
                        model.Hub.Disconnect(key);
                        model.Hub.OnDisposed -= key;
                    }

                    model.Hub.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => model.Hub.Connect(new object()));
                    Assert.Throws<ObjectDisposedException>(() => model.Hub.OnDisposed += model.Keys[0]);
                    Assert.Throws<ObjectDisposedException>(() => model.SavedBuilder.OnDisposed(model.Keys[0]));
                };

                model.Hub.OnDisposed += callback;
                model.ExtraKeys.Add(callback);
                DisposeHub(model);
                Assert.That(calls, Is.EqualTo(1));
            }

            private static void CheckDisposed(HubModel model)
            {
                Assert.That(model.Hub.IsDisposed, Is.True);
                Assert.Throws<ObjectDisposedException>(() => model.Hub.Connect(model.Keys[0]));
                Assert.Throws<ObjectDisposedException>(() => model.Hub.OnDisposed += model.Keys[0]);
                Assert.Throws<ObjectDisposedException>(() => model.SavedBuilder.OnDisposed(model.Keys[0]));
                foreach (var key in model.Keys)
                {
                    model.Hub.Disconnect(key);
                    model.Hub.OnDisposed -= key;
                }

                model.Hub.Dispose();
                AssertState(model);
            }

            private static void AssertState(HubModel model)
            {
                Assert.That(model.Hub.IsDisposed, Is.EqualTo(model.Disposed));
                Assert.That(model.Calls, Is.EqualTo(model.ExpectedCalls));
            }

            public void Dispose()
            {
                // UniTest replays prefixes on separate subjects; unfinished prefixes also need cleanup.
                foreach (var model in _active.Keys)
                {
                    foreach (var key in model.Keys) model.Hub.Disconnect(key);
                    foreach (var key in model.ExtraKeys) model.Hub.Disconnect(key);
                    model.Hub.Dispose();
                }

                _active.Clear();
            }
        }


        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // U1-U5: All registration, disposal, failure and restart operations are exercised directly.
        [Test]
        public async Task FixedRegistrationLifetimes()
        {
            using var project = new HubProject();
            var result = await project.Execute(_fixedPath.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_fixedPath.Length + 1));
        }

        // U1-U5: Registrations and notification counts accumulate across successive hub lifetimes.
        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task RandomContinuousRegistrationLifetimes(int seed)
        {
            using var project = new HubProject(continuous: true);
            await RandomContinuousTest.RunAsync(project, seed);
        }
    }
}
