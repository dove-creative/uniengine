using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniTest;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class HandleTokenStateProjectTests
    {
        // Internal
        private enum Policy { Rethrow, Handled, ThrowingHandler }

        private static readonly int[] _initialCallbacks = { 0, 2, 1, 0, 3, 2, 1 };
        private static readonly string[] _handlePath =
        {
            "Observe", "Describe", "PolicyHandled", "Dispose", "CheckDisposed", "RestartCallbacks",
            "PolicyThrow", "Dispose", "CheckDisposed", "RestartCallbacks", "PolicyRethrow", "Dispose",
            "CheckDisposed", "RestartEmpty", "Observe", "Describe", "PolicyHandled", "Dispose", "CheckDisposed"
        };
        private static readonly string[] _tokenPath =
        {
            "Observe", "Describe", "Add0", "Add1", "AddFailure", "AddReentrant", "AddPair", "Remove0",
            "RemovePair", "RemoveMissing", "Null", "PolicyHandled", "Dispose", "CheckDisposed",
            "RestartEmpty", "Add0", "AddPair", "RemovePair", "Remove0", "Null", "Dispose", "CheckDisposed",
            "RestartCallbacks", "PolicyThrow", "Dispose", "CheckDisposed", "RestartCallbacks", "PolicyRethrow",
            "Dispose", "CheckDisposed", "RestartEmpty", "PolicyHandled", "Dispose", "CheckDisposed"
        };

        private class LifetimeModel : Model
        {
            public Handle Handle => (Handle)Subject;
            public Token Token => Subject as Token;
            public Action[] Callbacks { get; } = new Action[5];
            public List<int> Expected { get; } = new();
            public List<int> Calls { get; } = new();
            public List<int> ExpectedCalls { get; } = new();
            public List<Exception> Reports { get; } = new();
            public int ExpectedReports { get; set; }
            public bool Disposed { get; set; }
            public string Description { get; set; }
            public Policy Policy { get; set; }
            public ApplicationException Failure { get; } = new("callback");
            public InvalidOperationException PolicyFailure { get; } = new("policy");

            public LifetimeModel()
            {
                for (var index = 0; index < Callbacks.Length; index++)
                {
                    var id = index;
                    Callbacks[index] = () =>
                    {
                        Assert.That(Handle.IsDisposed, Is.True);
                        Calls.Add(id);
                        if (id == 2) throw Failure;
                        if (id != 3) return;

                        Handle.Dispose();
                        if (Token == null) return;

                        Token.Action -= Callbacks[0];
                        Token.Action -= null;
                        Assert.Throws<ObjectDisposedException>(() => Token.Action += Callbacks[0]);
                        Assert.Throws<ObjectDisposedException>(() => Token.Action += null);
                    };
                }
            }
        }

        private class LifetimeProject : CompactProject<LifetimeModel>, IDisposable
        {
            private readonly bool _token;
            private readonly bool _continuous;
            private readonly string[] _path;
            private readonly ConcurrentDictionary<LifetimeModel, byte> _active = new();

            public LifetimeProject(bool token, bool continuous = false) : base(ExecutionMode.Inline)
            {
                _token = token;
                _continuous = continuous;
                _path = token ? _tokenPath : _handlePath;
            }

            public override IEnumerable<CompactLab<LifetimeModel>> CreateCompactLabs(LifetimeModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return CheckedLab("Create", state => CreateHandle(state, empty: false));
                    yield break;
                }

                if (!_continuous && model.ExecutionCount > _path.Length) yield break;

                foreach (var lab in CreateOperations(model))
                {
                    if (_continuous || lab.Id == _path[model.ExecutionCount - 1]) yield return lab;
                }
            }

            private IEnumerable<CompactLab<LifetimeModel>> CreateOperations(LifetimeModel model)
            {
                if (model.Disposed)
                {
                    yield return CheckedLab("CheckDisposed", CheckDisposed);
                    yield return CheckedLab("RestartEmpty", state => CreateHandle(state, empty: true));
                    yield return CheckedLab("RestartCallbacks", state => CreateHandle(state, empty: false));
                    yield break;
                }

                yield return CheckedLab("Observe", _ => { });
                yield return CheckedLab("Describe", state =>
                {
                    state.Description = $"lifetime-{state.ExecutionCount}";
                    state.Handle.Description = state.Description;
                });
                yield return CheckedLab("PolicyRethrow", state => state.Policy = Policy.Rethrow);
                yield return CheckedLab("PolicyHandled", state => state.Policy = Policy.Handled);
                yield return CheckedLab("PolicyThrow", state => state.Policy = Policy.ThrowingHandler);
                yield return CheckedLab("Dispose", DisposeHandle);
                if (!_token) yield break;

                yield return CheckedLab("Add0", state => Add(state, 0));
                yield return CheckedLab("Add1", state => Add(state, 1));
                yield return CheckedLab("AddFailure", state => Add(state, 2));
                yield return CheckedLab("AddReentrant", state => Add(state, 3));
                yield return CheckedLab("AddPair", state =>
                {
                    state.Token.Action += state.Callbacks[0] + state.Callbacks[1];
                    state.Expected.AddRange(new[] { 0, 1 });
                });
                yield return CheckedLab("Remove0", state =>
                {
                    state.Token.Action -= state.Callbacks[0];
                    RemoveLast(state.Expected, 0);
                });
                yield return CheckedLab("RemovePair", state =>
                {
                    state.Token.Action -= state.Callbacks[0] + state.Callbacks[1];
                    RemoveLast(state.Expected, 0, 1);
                });
                yield return CheckedLab("RemoveMissing", state => state.Token.Action -= state.Callbacks[4]);
                yield return CheckedLab("Null", state =>
                {
                    state.Token.Action += null;
                    state.Token.Action -= null;
                });
            }

            private static CompactLab<LifetimeModel> CheckedLab(string id, Action<LifetimeModel> actor) =>
                new(id, actor: actor, asserter: AssertState);

            private void CreateHandle(LifetimeModel model, bool empty)
            {
                if (model.Subject != null) CheckDisposed(model);

                Action callbacks = null;
                model.Expected.Clear();
                if (!empty)
                {
                    foreach (var id in _initialCallbacks)
                    {
                        callbacks += model.Callbacks[id];
                        model.Expected.Add(id);
                    }
                }

                model.Subject = _token ? new Token(callbacks) : new Handle(callbacks);
                model.Disposed = false;
                model.Description = null;
                model.Policy = Policy.Rethrow;
                _active.TryAdd(model, 0);
            }

            private static void Add(LifetimeModel model, int id)
            {
                model.Token.Action += model.Callbacks[id];
                model.Expected.Add(id);
            }

            private static void RemoveLast(List<int> expected, params int[] group)
            {
                for (var start = expected.Count - group.Length; start >= 0; start--)
                {
                    var matches = true;
                    for (var offset = 0; offset < group.Length; offset++)
                        matches &= expected[start + offset] == group[offset];

                    if (!matches) continue;
                    expected.RemoveRange(start, group.Length);
                    return;
                }
            }

            private void DisposeHandle(LifetimeModel model)
            {
                Exception expectedFailure = null;
                foreach (var id in model.Expected)
                {
                    model.ExpectedCalls.Add(id);
                    if (id != 2) continue;

                    if (model.Policy != Policy.Rethrow) model.ExpectedReports++;
                    if (model.Policy == Policy.Handled) continue;
                    expectedFailure = model.Policy == Policy.Rethrow ? model.Failure : model.PolicyFailure;
                    break;
                }

                model.Disposed = true;
                model.Expected.Clear();
                if (model.Policy != Policy.Rethrow)
                {
                    ExceptionHandler.SetHandler(exception =>
                    {
                        // Only the injected callback failure belongs to the expected policy path.
                        if (!ReferenceEquals(exception, model.Failure)) throw exception;
                        model.Reports.Add(exception);
                        if (model.Policy == Policy.ThrowingHandler) throw model.PolicyFailure;
                    });
                }

                try
                {
                    if (expectedFailure == null) model.Handle.Dispose();
                    else
                    {
                        Assert.That(Assert.Throws(expectedFailure.GetType(), model.Handle.Dispose),
                            Is.SameAs(expectedFailure));
                    }
                }
                finally
                {
                    ExceptionHandler.SetHandler(null);
                    if (model.Handle.IsDisposed) _active.TryRemove(model, out _);
                }
            }

            private static void CheckDisposed(LifetimeModel model)
            {
                Assert.That(model.Handle.IsDisposed, Is.True);
                model.Handle.Dispose();
                if (model.Token != null)
                {
                    Assert.Throws<ObjectDisposedException>(() => model.Token.Action += model.Callbacks[0]);
                    Assert.Throws<ObjectDisposedException>(() => model.Token.Action += null);
                    model.Token.Action -= model.Callbacks[0];
                    model.Token.Action -= null;
                }

                AssertState(model);
            }

            private static void AssertState(LifetimeModel model)
            {
                Assert.That(model.Handle.IsDisposed, Is.EqualTo(model.Disposed));
                Assert.That(model.Handle.Description, Is.EqualTo(model.Description));
                Assert.That(model.Calls, Is.EqualTo(model.ExpectedCalls));
                Assert.That(model.Reports.Count, Is.EqualTo(model.ExpectedReports));
                foreach (var report in model.Reports) Assert.That(report, Is.SameAs(model.Failure));
            }

            public void Dispose()
            {
                // Replay prefixes also create live subjects; finish every remaining lifetime.
                try
                {
                    foreach (var model in _active.Keys)
                    {
                        ExceptionHandler.SetHandler(exception =>
                        {
                            if (!ReferenceEquals(exception, model.Failure)) throw exception;
                        });
                        model.Handle.Dispose();
                    }
                }
                finally
                {
                    _active.Clear();
                    ExceptionHandler.SetHandler(null);
                }
            }
        }


        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // U1-U3: Fixed paths exercise all applicable operations and every failure policy.
        [TestCase(false)]
        [TestCase(true)]
        public async Task FixedLifetimes(bool token)
        {
            using var project = new LifetimeProject(token);
            var depth = (token ? _tokenPath.Length : _handlePath.Length) + 1;
            var result = await project.Execute(depth);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(depth));
        }

        // U1-U3: Registration and disposal histories accumulate across independently created instances.
        [Category("RandomContinuous")]
        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        public async Task RandomContinuousLifetimes(bool token, int seed)
        {
            using var project = new LifetimeProject(token, continuous: true);
            await RandomContinuousTest.RunAsync(project, seed);
        }
    }
}
