using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamWhenMutationProjectTests
    {
        // Internal
        private enum Mutation { None, Add, Replace, Remove }

        private class MutationModel : Model
        {
            public EventStream<int?> Source { get; private set; }

            private readonly List<Handle> _handles = new();
            private readonly List<string> _deliveries = new();
            private readonly List<string> _completions = new();
            private readonly List<Exception> _errors = new();
            private Handle _first;
            private Handle _alias;
            private Handle _other;
            private Mutation _mutation;
            private int _mutationsPerformed;


            // Content --------------------
            // Registration and publication
            public void RegisterRoutes()
            {
                Source = new EventStream<int?>();
                Subject = Source;
                _first = Subscribe(1, "first");
                _alias = Subscribe(1, "alias");
                _other = Subscribe(2, "other");
                _handles.Add((Handle)Source.Subscribe(
                    _ =>
                    {
                        _deliveries.Add("root");
                        MutateSubscriptions();
                    },
                    () => _completions.Add("root"),
                    _errors.Add));
                Source.OnNext(1);
            }

            public void AssertRegistered()
            {
                Assert.That(_deliveries, Is.EquivalentTo(new[] { "root", "first", "alias" }));
                Assert.That(Source.NodeCount, Is.EqualTo(1));
                Assert.That(_handles.All(handle => !handle.IsDisposed), Is.True);
                Assert.That(_mutationsPerformed, Is.Zero);
                AssertActive();
            }

            public void PublishWithMutation(Mutation mutation)
            {
                _mutation = mutation;
                _deliveries.Clear();
                Source.OnNext(1);
            }

            private void MutateSubscriptions()
            {
                if (_mutation == Mutation.None || _mutationsPerformed != 0) return;
                _mutationsPerformed++;

                switch (_mutation)
                {
                    case Mutation.Add:
                        Subscribe(1, "late");
                        Subscribe(null, "null");
                        break;
                    case Mutation.Replace:
                        _first.Dispose();
                        _alias.Dispose();
                        Subscribe(1, "replacement");
                        break;
                    case Mutation.Remove:
                        _first.Dispose();
                        _alias.Dispose();
                        break;
                }
            }

            public void AssertMutated()
            {
                Assert.That(_deliveries, Is.EquivalentTo(new[] { "root", "first", "alias" }));
                AssertMutationState();
            }

            public void PublishAfterMutation()
            {
                _deliveries.Clear();
                Source.OnNext(1);
                Source.OnNext(null);
                Source.OnNext(2);
            }

            public void AssertNextPublication()
            {
                var expected = _mutation switch
                {
                    Mutation.Add => new[] { "root", "first", "alias", "late", "root", "null", "root", "other" },
                    Mutation.Replace => new[] { "root", "replacement", "root", "root", "other" },
                    _ => new[] { "root", "root", "root", "other" }
                };
                Assert.That(_deliveries, Is.EquivalentTo(expected));
                AssertMutationState();
            }

            private void AssertMutationState()
            {
                Assert.That(Source.NodeCount, Is.EqualTo(1));
                Assert.That(_mutationsPerformed, Is.EqualTo(1));
                Assert.That(_first.IsDisposed, Is.EqualTo(_mutation != Mutation.Add));
                Assert.That(_alias.IsDisposed, Is.EqualTo(_mutation != Mutation.Add));
                Assert.That(_other.IsDisposed, Is.False);
                Assert.That(_handles.Count(handle => !handle.IsDisposed), Is.EqualTo(CompletionNames().Length));
                AssertActive();
            }

            private void AssertActive()
            {
                Assert.That(Source.IsDisposed, Is.False);
                Assert.That(_completions, Is.Empty);
                Assert.That(_errors, Is.Empty);
            }

            // Termination
            public void ReleaseAll()
            {
                foreach (var handle in _handles) handle.Dispose();
                PublishAfterMutation();
            }

            public void CompleteAll()
            {
                _deliveries.Clear();
                Source.OnCompleted();
            }

            public void AssertEnded(bool completed)
            {
                Assert.That(_deliveries, Is.Empty);
                Assert.That(_completions, Is.EquivalentTo(completed ? CompletionNames() : Array.Empty<string>()));
                Assert.That(_errors, Is.Empty);
                Assert.That(_handles.All(handle => handle.IsDisposed), Is.True);
                Assert.That(Source.NodeCount, Is.Zero);
                Assert.That(Source.IsDisposed, Is.EqualTo(completed));
                if (completed) Assert.Throws<ObjectDisposedException>(() => Source.OnNext(1));
            }

            private string[] CompletionNames() => _mutation switch
            {
                Mutation.Add => new[] { "root", "first", "alias", "late", "other", "null" },
                Mutation.Replace => new[] { "root", "other", "replacement" },
                _ => new[] { "root", "other" }
            };

            private Handle Subscribe(int? target, string name)
            {
                var handle = (Handle)Source.When(target).Subscribe(
                    _ => _deliveries.Add(name),
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
                            actor: state => state.PublishAfterMutation(),
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
        // UWM1-UWM6: Replay each in-flight mutation, later values, and both cleanup endings.
        [Test]
        public async Task WhenPublicationMutationsSucceedThroughUniTest()
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

                Assert.That(report.Nodes.Where(node => endings.Contains(node.LabId)).Select(node => node.History),
                    Is.EquivalentTo(expected));
                TestContext.WriteLine($"When publication mutations: {expected.Count} terminal histories verified.");
            }
            finally
            {
                DisposeReplaySubjects(result);
            }
        }

        // UWM1-UWM6: Reuse the bounded assertions throughout independent random lifecycles.
        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task WhenPublicationMutationsContinueWithRandomSeed(int seed)
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

        private static void DisposeCycle(MutationModel model) => model.Source?.Dispose();
    }
}
