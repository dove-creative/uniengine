using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamMergeStateProjectTests
    {
        // Internal
        private class MergeModel : Model
        {
            public EventStream<int> Left { get; private set; }
            public EventStream<int> Right { get; private set; }
            public EventStream<int> Merged { get; private set; }
            public Handle Subscription { get; private set; }

            private readonly List<int> _values = new();
            private readonly List<int> _afterRelease = new();
            private readonly List<Exception> _errors = new();
            private readonly Exception _failure = new ApplicationException("remaining input");
            private int _completions;


            public void ConnectPair()
            {
                Left = new EventStream<int>();
                Right = new EventStream<int>();
                Merged = Left.Merge(Right);
                Subject = Merged;
                Subscription = (Handle)Merged.Subscribe(_values.Add, () => _completions++, _errors.Add);
                Left.OnNext(1);
                Right.OnNext(2);
            }

            public void AssertConnected()
            {
                Assert.That(_values, Is.EqualTo(new[] { 1, 2 }));
                Assert.That(Left.NodeCount, Is.EqualTo(1));
                Assert.That(Right.NodeCount, Is.EqualTo(1));
                Assert.That(Merged.IsDisposed, Is.False);
                Assert.That(Subscription.IsDisposed, Is.False);
                Assert.That(_completions, Is.Zero);
                Assert.That(_errors, Is.Empty);
            }

            public void AssertRemaining(int[] values)
            {
                Assert.That(_values, Is.EqualTo(values));
                Assert.That(Left.IsDisposed, Is.True);
                Assert.That(Left.NodeCount, Is.Zero);
                Assert.That(Right.IsDisposed, Is.False);
                Assert.That(Right.NodeCount, Is.EqualTo(1));
                Assert.That(Merged.IsDisposed, Is.False);
                Assert.That(Subscription.IsDisposed, Is.False);
                Assert.That(_completions, Is.Zero);
                Assert.That(_errors, Is.Empty);
            }

            public void ContinueRemaining()
            {
                Right.OnNext(3);
                Merged.OnNext(4);
            }

            public void EndRemaining(string ending)
            {
                switch (ending)
                {
                    case "CompleteLast": Right.OnCompleted(); break;
                    case "DisposeLast": Right.Dispose(); break;
                    case "ErrorLast": Right.OnError(_failure); break;
                    case "ReleaseLast":
                        Subscription.Dispose();
                        using (Right.Subscribe(_afterRelease.Add)) Right.OnNext(5);
                        break;
                }
            }

            public void AssertEnded(string ending)
            {
                Assert.That(_values, Is.EqualTo(new[] { 1, 2, 3, 4 }));
                AssertReleasedConnections();
                Assert.That(Left.IsDisposed, Is.True);
                Assert.That(Right.IsDisposed, Is.EqualTo(ending != "ReleaseLast"));
                Assert.That(_completions, Is.EqualTo(ending == "CompleteLast" ? 1 : 0));
                Assert.That(_errors, Is.EqualTo(ending == "ErrorLast" ? new[] { _failure } : Array.Empty<Exception>()));
                Assert.That(_afterRelease, Is.EqualTo(ending == "ReleaseLast" ? new[] { 5 } : Array.Empty<int>()));
            }

            public void ReleaseBothInputs()
            {
                Subscription.Dispose();
                using var left = Left.Subscribe(_afterRelease.Add);
                using var right = Right.Subscribe(_afterRelease.Add);
                Left.OnNext(5);
                Right.OnNext(6);
            }

            public void AssertReleasedPair()
            {
                Assert.That(_values, Is.EqualTo(new[] { 1, 2 }));
                AssertReleasedConnections();
                Assert.That(Left.IsDisposed || Right.IsDisposed, Is.False);
                Assert.That(_completions, Is.Zero);
                Assert.That(_errors, Is.Empty);
                Assert.That(_afterRelease, Is.EqualTo(new[] { 5, 6 }));
            }

            private void AssertReleasedConnections()
            {
                Assert.That(Merged.IsDisposed && Subscription.IsDisposed, Is.True);
                Assert.That(Merged.NodeCount, Is.Zero);
                Assert.That(Left.NodeCount, Is.Zero);
                Assert.That(Right.NodeCount, Is.Zero);
                Assert.Throws<ObjectDisposedException>(() => Merged.Subscribe(_ => { }));
            }
        }

        private class MergeProject : CompactProject<MergeModel>
        {
            public MergeProject() : base(ExecutionMode.Inline) { }

            public override IEnumerable<CompactLab<MergeModel>> CreateCompactLabs(MergeModel model) =>
                CreateLifecycleLabs(model.ExecutionCount);

            public static IEnumerable<CompactLab<MergeModel>> CreateLifecycleLabs(int step)
            {
                switch (step)
                {
                    case 0:
                        yield return new CompactLab<MergeModel>(
                            "ConnectPair",
                            actor: state => state.ConnectPair(),
                            asserter: state => state.AssertConnected());
                        break;
                    case 1:
                        yield return new CompactLab<MergeModel>(
                            "CompleteFirst",
                            actor: state => state.Left.OnCompleted(),
                            asserter: state => state.AssertRemaining(new[] { 1, 2 }));
                        yield return new CompactLab<MergeModel>(
                            "DisposeFirst",
                            actor: state => state.Left.Dispose(),
                            asserter: state => state.AssertRemaining(new[] { 1, 2 }));
                        yield return new CompactLab<MergeModel>(
                            "ReleaseBothInputs",
                            actor: state => state.ReleaseBothInputs(),
                            asserter: state => state.AssertReleasedPair(),
                            toUncontinuable: true);
                        break;
                    case 2:
                        yield return new CompactLab<MergeModel>(
                            "ContinueRemaining",
                            actor: state => state.ContinueRemaining(),
                            asserter: state => state.AssertRemaining(new[] { 1, 2, 3, 4 }));
                        break;
                    case 3:
                        foreach (var ending in new[] { "CompleteLast", "DisposeLast", "ErrorLast", "ReleaseLast" })
                        {
                            yield return new CompactLab<MergeModel>(
                                ending,
                                actor: state => state.EndRemaining(ending),
                                asserter: state => state.AssertEnded(ending),
                                toUncontinuable: true);
                        }
                        break;
                }
            }
        }


        // Content
        // UM1-UM9: Replay both partial endings, all final signals, and release with both inputs alive.
        [Test]
        public async Task MergeLifetimesSucceedThroughUniTest()
        {
            var result = await new MergeProject().Execute(4, stopOnFailure: false);
            try
            {
                var report = result.GetReport();
                Assert.That(result.AllSucceed(out var cancelled), Is.True, report.ToJson());
                Assert.That(cancelled, Is.False);
                var endings = new[] { "CompleteLast", "DisposeLast", "ErrorLast", "ReleaseLast" };
                var expected = new List<string> { "ConnectPair/ReleaseBothInputs" };
                foreach (var first in new[] { "CompleteFirst", "DisposeFirst" })
                {
                    foreach (var ending in endings)
                        expected.Add($"ConnectPair/{first}/ContinueRemaining/{ending}");
                }

                Assert.That(report.Nodes
                    .Where(node => endings.Contains(node.LabId) || node.LabId == "ReleaseBothInputs")
                    .Select(node => node.History), Is.EquivalentTo(expected));
            }
            finally
            {
                DisposeReplaySubjects(result);
            }
        }

        // UM1-UM9: Continue through repeated lifecycles with the same assertions as the bounded test.
        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task MergeLifetimesContinueWithRandomSeed(int seed)
        {
            using var project = new EventStreamLifecycleProject<MergeModel>(
                DisposeCycle,
                ("", MergeProject.CreateLifecycleLabs));
            await RandomContinuousTest.RunAsync(project, seed);
        }

        private static void DisposeReplaySubjects(Node<MergeModel> node)
        {
            DisposeCycle(node.Model);

            foreach (var child in node.Afters) DisposeReplaySubjects(child);
        }

        private static void DisposeCycle(MergeModel model)
        {
            model.Merged?.Dispose();
            model.Left?.Dispose();
            model.Right?.Dispose();
        }
    }
}
