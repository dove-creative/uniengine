using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamFlagsMutationProjectTests
    {
        // Internal
        [Flags]
        private enum EventKinds { Shape = 1, Location = 2, Payload = 4 }

        private enum Mutation { None, Add, Replace, Remove }

        private class MutationModel : Model
        {
            public EventStream<ulong> Source { get; private set; }

            private readonly List<Handle> _handles = new();
            private readonly List<string> _deliveries = new();
            private readonly List<string> _completions = new();
            private readonly List<Exception> _errors = new();
            private Handle _shape;
            private Handle _location;
            private Handle _both;
            private Mutation _mutation;
            private int _mutationsPerformed;


            public void RegisterRoutes()
            {
                Source = new EventStream<ulong>();
                Subject = Source;
                _shape = Subscribe(EventKinds.Shape, "shape", MutateSubscriptions);
                _location = Subscribe(EventKinds.Location, "location");
                _both = Subscribe(EventKinds.Shape | EventKinds.Location, "both");
                PublishNext();
            }

            public void AssertRegistered()
            {
                Assert.That(_deliveries, Is.EqualTo(new[] { "shape", "location", "both" }));
                Assert.That(Source.NodeCount, Is.EqualTo(1));
                Assert.That(_handles.All(handle => !handle.IsDisposed), Is.True);
                Assert.That(_mutationsPerformed, Is.Zero);
                AssertActive();
            }

            public void PublishWithMutation(Mutation mutation)
            {
                _mutation = mutation;
                PublishNext();
            }

            private void MutateSubscriptions()
            {
                if (_mutation == Mutation.None || _mutationsPerformed != 0) return;
                _mutationsPerformed++;

                switch (_mutation)
                {
                    case Mutation.Add:
                        Subscribe(EventKinds.Location, "late");
                        Subscribe(EventKinds.Payload, "new");
                        break;
                    case Mutation.Replace:
                        _location.Dispose();
                        Subscribe(EventKinds.Location, "replacement");
                        break;
                    case Mutation.Remove:
                        _shape.Dispose();
                        _location.Dispose();
                        break;
                }
            }

            public void PublishNext()
            {
                _deliveries.Clear();
                Source.OnNext(7);
            }

            public void AssertMutated()
            {
                var expected = _mutation == Mutation.Add
                    ? new[] { "shape", "location", "both" }
                    : new[] { "shape", "both" };
                Assert.That(_deliveries, Is.EqualTo(expected));
                AssertMutationState();
            }

            public void AssertNextPublication()
            {
                Assert.That(_deliveries, Is.EqualTo(ActiveNames()));
                AssertMutationState();
            }

            private void AssertMutationState()
            {
                Assert.That(Source.NodeCount, Is.EqualTo(1));
                Assert.That(_mutationsPerformed, Is.EqualTo(1));
                Assert.That(_shape.IsDisposed, Is.EqualTo(_mutation == Mutation.Remove));
                Assert.That(_location.IsDisposed, Is.EqualTo(_mutation != Mutation.Add));
                Assert.That(_both.IsDisposed, Is.False);
                Assert.That(_handles.Count(handle => !handle.IsDisposed), Is.EqualTo(ActiveNames().Length));
                AssertActive();
            }

            private void AssertActive()
            {
                Assert.That(Source.IsDisposed, Is.False);
                Assert.That(_completions, Is.Empty);
                Assert.That(_errors, Is.Empty);
            }

            public void ReleaseAll()
            {
                foreach (var handle in _handles) handle.Dispose();

                PublishNext();
            }

            public void CompleteAll()
            {
                _deliveries.Clear();
                Source.OnCompleted();
            }

            public void AssertEnded(bool completed)
            {
                Assert.That(_deliveries, Is.Empty);
                Assert.That(_completions, Is.EqualTo(completed ? ActiveNames() : Array.Empty<string>()));
                Assert.That(_errors, Is.Empty);
                Assert.That(_handles.All(handle => handle.IsDisposed), Is.True);
                Assert.That(Source.NodeCount, Is.Zero);
                Assert.That(Source.IsDisposed, Is.EqualTo(completed));

                if (completed) Assert.Throws<ObjectDisposedException>(() => Source.OnNext(7));
            }

            private string[] ActiveNames() => _mutation switch
            {
                Mutation.Add => new[] { "shape", "location", "late", "both", "new" },
                Mutation.Replace => new[] { "shape", "both", "replacement" },
                _ => new[] { "both" }
            };

            private Handle Subscribe(EventKinds mask, string name, Action onNext = null)
            {
                var handle = (Handle)Source.ForFlags(mask).Subscribe(
                    _ =>
                    {
                        _deliveries.Add(name);
                        onNext?.Invoke();
                    },
                    () => _completions.Add(name),
                    _errors.Add);
                _handles.Add(handle);
                return handle;
            }
        }

        private class MutationProject : CompactProject<MutationModel>
        {
            public MutationProject() : base(ExecutionMode.Inline) { }

            public override IEnumerable<CompactLab<MutationModel>> CreateCompactLabs(MutationModel model) =>
                CreateLifecycleLabs(model.ExecutionCount);

            public static IEnumerable<CompactLab<MutationModel>> CreateLifecycleLabs(int step)
            {
                switch (step)
                {
                    case 0:
                        yield return new CompactLab<MutationModel>(
                            "RegisterRoutes",
                            actor: state => state.RegisterRoutes(),
                            asserter: state => state.AssertRegistered());
                        break;
                    case 1:
                        foreach (var mutation in new[] { Mutation.Add, Mutation.Replace, Mutation.Remove })
                        {
                            yield return new CompactLab<MutationModel>(
                                $"{mutation}DuringPublication",
                                actor: state => state.PublishWithMutation(mutation),
                                asserter: state => state.AssertMutated());
                        }
                        break;
                    case 2:
                        yield return new CompactLab<MutationModel>(
                            "PublishAfterMutation",
                            actor: state => state.PublishNext(),
                            asserter: state => state.AssertNextPublication());
                        break;
                    case 3:
                        yield return new CompactLab<MutationModel>(
                            "ReleaseAll",
                            actor: state => state.ReleaseAll(),
                            asserter: state => state.AssertEnded(false),
                            toUncontinuable: true);
                        yield return new CompactLab<MutationModel>(
                            "CompleteAll",
                            actor: state => state.CompleteAll(),
                            asserter: state => state.AssertEnded(true),
                            toUncontinuable: true);
                        break;
                }
            }
        }


        // Content
        // UFM1-UFM6: Replay each in-flight mutation, its next publication, and both cleanup endings.
        [Test]
        public async Task FlagsPublicationMutationsSucceedThroughUniTest()
        {
            var result = await new MutationProject().Execute(4, stopOnFailure: false);
            try
            {
                var report = result.GetReport();
                Assert.That(result.AllSucceed(out var cancelled), Is.True, report.ToJson());
                Assert.That(cancelled, Is.False);
                var endings = new[] { "ReleaseAll", "CompleteAll" };
                var expected = new List<string>();
                foreach (var mutation in new[] { "Add", "Replace", "Remove" })
                {
                    foreach (var ending in endings)
                        expected.Add($"RegisterRoutes/{mutation}DuringPublication/PublishAfterMutation/{ending}");
                }

                Assert.That(report.Nodes
                    .Where(node => endings.Contains(node.LabId))
                    .Select(node => node.History), Is.EquivalentTo(expected));
            }
            finally
            {
                DisposeReplaySubjects(result);
            }
        }

        // UFM1-UFM6: Continue through repeated lifecycles with the same assertions as the bounded test.
        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task FlagsPublicationMutationsContinueWithRandomSeed(int seed)
        {
            using var project = new EventStreamLifecycleProject<MutationModel>(
                DisposeCycle,
                ("", MutationProject.CreateLifecycleLabs));
            await RandomContinuousTest.RunAsync(project, seed);
        }

        private static void DisposeReplaySubjects(Node<MutationModel> node)
        {
            DisposeCycle(node.Model);

            foreach (var child in node.Afters) DisposeReplaySubjects(child);
        }

        private static void DisposeCycle(MutationModel model)
        {
            model.Source?.Dispose();
        }
    }
}
