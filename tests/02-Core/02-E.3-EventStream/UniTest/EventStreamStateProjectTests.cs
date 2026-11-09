using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamStateProjectTests
    {
        // Internal
        private class StreamModel : Model
        {
            public EventStream<int> Source { get; private set; }
            public EventStream<int> Branch { get; private set; }
            public IDisposable First { get; private set; }
            public IDisposable Second { get; private set; }
            public List<int> SourceAfterDetach { get; } = new();

            private readonly List<int> _firstValues = new();
            private readonly List<int> _secondValues = new();
            private int _predicateCalls;
            private int _firstCompletions;
            private int _secondCompletions;

            public void SubscribePair()
            {
                Source = new EventStream<int>();
                Subject = Source;
                Branch = Source.Branch();

                var even = Branch.Where(value =>
                {
                    _predicateCalls++;
                    return value % 2 == 0;
                });

                First = even.Subscribe(_firstValues.Add, () => _firstCompletions++, null);
                Second = even.Subscribe(_secondValues.Add, () => _secondCompletions++, null);
                Source.OnNext(2);
            }

            public void AssertDeliveries(int predicateCalls, int[] secondValues, int secondCompletions = 0)
            {
                Assert.That(_firstValues, Is.EqualTo(new[] { 2 }));
                Assert.That(_secondValues, Is.EqualTo(secondValues));
                Assert.That(_predicateCalls, Is.EqualTo(predicateCalls));
                Assert.That(_firstCompletions, Is.Zero);
                Assert.That(_secondCompletions, Is.EqualTo(secondCompletions));
            }
        }

        private class StreamProject : CompactProject<StreamModel>
        {
            public StreamProject() : base(ExecutionMode.Inline) { }

            public override IEnumerable<CompactLab<StreamModel>> CreateCompactLabs(StreamModel model)
            {
                switch (model.ExecutionCount)
                {
                    case 0:
                        yield return new CompactLab<StreamModel>(
                            "SubscribePair",
                            actor: state => state.SubscribePair(),
                            asserter: state =>
                            {
                                Assert.That(state.Source.NodeCount, Is.EqualTo(1));
                                Assert.That(state.Branch.NodeCount, Is.EqualTo(1));
                                state.AssertDeliveries(1, new[] { 2 });
                            });

                        break;
                    case 1:
                        yield return new CompactLab<StreamModel>(
                            "ReleaseFirst",
                            actor: state =>
                            {
                                state.First.Dispose();
                                state.Source.OnNext(3);
                                state.Source.OnNext(4);
                            },
                            asserter: state =>
                            {
                                Assert.That(state.Branch.IsDisposed, Is.False);
                                state.AssertDeliveries(3, new[] { 2, 4 });
                            });

                        break;
                    case 2:
                        yield return new CompactLab<StreamModel>(
                            "PublishWhileDisabled",
                            actor: state =>
                            {
                                state.Branch.Enabled = false;
                                state.Source.OnNext(6);
                            },
                            asserter: state =>
                            {
                                Assert.That(state.Branch.Enabled, Is.False);
                                state.AssertDeliveries(3, new[] { 2, 4 });
                            });

                        break;
                    case 3:
                        yield return new CompactLab<StreamModel>(
                            "ResumePublication",
                            actor: state =>
                            {
                                state.Branch.Enabled = true;
                                state.Source.OnNext(8);
                            },
                            asserter: state => state.AssertDeliveries(4, new[] { 2, 4, 8 }));

                        // U2: Completion still reaches the remaining observer while values are disabled.
                        yield return new CompactLab<StreamModel>(
                            "CompleteWhileDisabled",
                            actor: state => state.Source.OnCompleted(),
                            asserter: state =>
                            {
                                Assert.That(state.Source.IsDisposed, Is.True);
                                Assert.That(state.Branch.IsDisposed, Is.True);
                                state.AssertDeliveries(3, new[] { 2, 4 }, secondCompletions: 1);
                            },
                            toUncontinuable: true);

                        break;
                    case 4:
                        // U1: The last release detaches the branch; the source remains usable independently.
                        yield return new CompactLab<StreamModel>(
                            "ReleaseLast",
                            actor: state =>
                            {
                                state.Second.Dispose();
                                using var fresh = state.Source.Subscribe(state.SourceAfterDetach.Add);
                                state.Source.OnNext(10);
                            },
                            asserter: state =>
                            {
                                Assert.That(state.Branch.IsDisposed, Is.True);
                                Assert.That(state.Source.IsDisposed, Is.False);
                                Assert.That(state.Source.NodeCount, Is.Zero);
                                Assert.That(state.SourceAfterDetach, Is.EqualTo(new[] { 10 }));
                                state.AssertDeliveries(4, new[] { 2, 4, 8 });
                            },
                            toUncontinuable: true);

                        break;
                }
            }
        }


        // Content
        [Test]
        public async Task BranchLifecycleAndDisabledCompletionSucceedThroughUniTest()
        {
            var result = await new StreamProject().Execute(5, stopOnFailure: false);
            try
            {
                var report = result.GetReport();
                Assert.That(result.AllSucceed(out var cancelled), Is.True, report.ToJson());
                Assert.That(cancelled, Is.False);
                const string prefix = "SubscribePair/ReleaseFirst/PublishWhileDisabled/";

                Assert.That(
                    report.Nodes.Where(node => node.LabId == "ReleaseLast" || node.LabId == "CompleteWhileDisabled")
                        .Select(node => node.History),
                    Is.EquivalentTo(new[]
                    {
                        prefix + "ResumePublication/ReleaseLast",
                        prefix + "CompleteWhileDisabled",
                    }));
            }
            finally
            {
                DisposeReplaySubjects(result);
            }
        }

        private static void DisposeReplaySubjects(Node<StreamModel> node)
        {
            node.Model.Branch?.Dispose();
            node.Model.Source?.Dispose();

            foreach (var child in node.Afters) DisposeReplaySubjects(child);
        }
    }
}
