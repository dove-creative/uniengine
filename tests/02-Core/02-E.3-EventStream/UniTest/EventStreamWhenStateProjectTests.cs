using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamWhenStateProjectTests
    {
        // Internal
        private enum Phase { Update, Render }

        private class WhenModel : Model
        {
            public EventStream<string> Source { get; private set; }
            public EventStream<string> Branch { get; private set; }

            private readonly List<string> _deliveries = new();
            private readonly List<string> _completions = new();
            private readonly List<Exception> _errors = new();
            private readonly List<string> _afterRelease = new();
            private readonly Exception _failure = new ApplicationException("when source");
            private Handle _first;
            private Handle _alias;
            private Handle _render;
            private Handle _unmatched;
            private bool _resumed;
            private bool _aliveAfterGroupRelease;
            private bool _aliveWithUnmatchedOnly;
            private int _nodesAfterGroupRelease;
            private int _nodesWithUnmatchedOnly;
            private int _freshNodes;


            // Content --------------------
            // Registration and input
            public void RegisterSharedTargets()
            {
                Source = new EventStream<string>();
                Branch = Source.When("one").Branch();
                Subject = Branch;
                _render = Observe(Branch.ForOrder(Phase.Render).When(new string(new[] { 'o', 'n', 'e' })), "render");
                _unmatched = Observe(Branch.ForOrder(Phase.Render).When(null), "unmatched");
                _first = Observe(Branch.ForOrder(Phase.Update).When("one"), "first");
                _alias = Observe(Branch.ForOrder(Phase.Update).When(new string(new[] { 'o', 'n', 'e' })), "alias");
                Source.OnNext(null);
                Source.OnNext("other");
                Source.OnNext("one");
            }

            private Handle Observe(EventPath<string> path, string name) =>
                (Handle)path.Subscribe(
                    value => _deliveries.Add($"{name}:{value}"),
                    () => _completions.Add(name),
                    _errors.Add);

            public void ReleaseFirst()
            {
                _first.Dispose();
                Source.OnNext("one");
            }

            public void DisableInputs()
            {
                Branch.Enabled = false;
                Source.OnNext("one");
                Branch.OnNext("one");
            }

            public void ResumePublication()
            {
                Branch.Enabled = true;
                _resumed = true;
                Source.OnNext("one");
            }

            public void AssertActive(bool firstReleased, bool enabled)
            {
                AssertValues(firstReleased);
                Assert.That(Source.NodeCount, Is.EqualTo(2));
                Assert.That(Branch.NodeCount, Is.EqualTo(3));
                Assert.That(Source.IsDisposed || Branch.IsDisposed, Is.False);
                Assert.That(Branch.Enabled, Is.EqualTo(enabled));
                Assert.That(_first.IsDisposed, Is.EqualTo(firstReleased));
                Assert.That(_alias.IsDisposed || _render.IsDisposed || _unmatched.IsDisposed, Is.False);
                Assert.That(_completions, Is.Empty);
                Assert.That(_errors, Is.Empty);
            }

            private void AssertValues(bool firstReleased)
            {
                var expected = new List<string> { "first:one", "alias:one", "render:one" };
                if (firstReleased) expected.AddRange(new[] { "alias:one", "render:one" });
                if (_resumed) expected.AddRange(new[] { "alias:one", "render:one" });

                Assert.That(_deliveries, Is.EqualTo(expected));
            }

            // Termination and reuse
            public void End(string ending)
            {
                switch (ending)
                {
                    case "Complete": Source.OnCompleted(); break;
                    case "Error": Source.OnError(_failure); break;
                    case "Dispose": Source.Dispose(); break;
                    case "ReleaseAll":
                        _alias.Dispose();
                        _nodesAfterGroupRelease = Branch.NodeCount;
                        _aliveAfterGroupRelease = !Branch.IsDisposed && Source.NodeCount == 2;
                        _render.Dispose();
                        _nodesWithUnmatchedOnly = Branch.NodeCount;
                        _aliveWithUnmatchedOnly = !Branch.IsDisposed && !_unmatched.IsDisposed;
                        _unmatched.Dispose();

                        using (Source.When(new string(new[] { 'o', 'n', 'e' })).Subscribe(_afterRelease.Add))
                        {
                            _freshNodes = Source.NodeCount;
                            Source.OnNext("other");
                            Source.OnNext("one");
                        }

                        break;
                }
            }

            public void AssertEnded(string ending)
            {
                AssertValues(true);
                Assert.That(Branch.IsDisposed, Is.True);
                Assert.That(_first.IsDisposed && _alias.IsDisposed && _render.IsDisposed && _unmatched.IsDisposed,
                    Is.True);
                Assert.That(Branch.NodeCount, Is.Zero);
                Assert.That(Source.NodeCount, Is.Zero);
                Assert.That(Source.IsDisposed, Is.EqualTo(ending != "ReleaseAll"));
                Assert.That(_completions, Is.EqualTo(ending == "Complete"
                    ? new[] { "alias", "render", "unmatched" }
                    : Array.Empty<string>()));
                Assert.That(_errors, Is.EqualTo(ending == "Error"
                    ? new[] { _failure, _failure, _failure }
                    : Array.Empty<Exception>()));
                Assert.Throws<ObjectDisposedException>(() => Branch.When("one").Subscribe(_ => { }));

                if (ending == "ReleaseAll")
                {
                    Assert.That(_aliveAfterGroupRelease && _aliveWithUnmatchedOnly, Is.True);
                    Assert.That(_nodesAfterGroupRelease, Is.EqualTo(2));
                    Assert.That(_nodesWithUnmatchedOnly, Is.EqualTo(2));
                    Assert.That(_freshNodes, Is.EqualTo(1));
                    Assert.That(_afterRelease, Is.EqualTo(new[] { "one" }));
                }
                else
                {
                    Assert.That(_afterRelease, Is.Empty);
                    Assert.Throws<ObjectDisposedException>(() => Source.OnNext("one"));
                }
            }
        }

        private class WhenProject : CompactProject<WhenModel>
        {
            public WhenProject() : base(ExecutionMode.Inline) { }

            public override IEnumerable<CompactLab<WhenModel>> CreateCompactLabs(WhenModel model) =>
                CreateLifecycleLabs(model.ExecutionCount);

            public static IEnumerable<CompactLab<WhenModel>> CreateLifecycleLabs(int step)
            {
                switch (step)
                {
                    case 0:
                        yield return new CompactLab<WhenModel>(
                            "RegisterSharedTargets",
                            actor: state => state.RegisterSharedTargets(),
                            asserter: state => state.AssertActive(false, true));
                        break;
                    case 1:
                        yield return new CompactLab<WhenModel>(
                            "ReleaseFirst",
                            actor: state => state.ReleaseFirst(),
                            asserter: state => state.AssertActive(true, true));
                        break;
                    case 2:
                        yield return new CompactLab<WhenModel>(
                            "DisableInputs",
                            actor: state => state.DisableInputs(),
                            asserter: state => state.AssertActive(true, false));
                        break;
                    case 3:
                        yield return new CompactLab<WhenModel>(
                            "ResumePublication",
                            actor: state => state.ResumePublication(),
                            asserter: state => state.AssertActive(true, true));

                        foreach (var lab in CreateEndings()) yield return lab;
                        break;
                    case 4:
                        foreach (var lab in CreateEndings()) yield return lab;
                        break;
                }
            }

            private static IEnumerable<CompactLab<WhenModel>> CreateEndings()
            {
                foreach (var ending in new[] { "Complete", "Error", "Dispose", "ReleaseAll" })
                {
                    yield return new CompactLab<WhenModel>(
                        ending,
                        actor: state => state.End(ending),
                        asserter: state => state.AssertEnded(ending),
                        toUncontinuable: true);
                }
            }
        }


        // Content
        // UW1-UW8: Replay shared targets, group isolation, disabled input, and every ending before or after resuming.
        [Test]
        public async Task WhenSharingAndLifetimesSucceedThroughUniTest()
        {
            var result = await new WhenProject().Execute(5, stopOnFailure: false);
            try
            {
                var report = result.GetReport();
                Assert.That(result.AllSucceed(out var cancelled), Is.True, report.ToJson());
                Assert.That(cancelled, Is.False);
                const string prefix = "RegisterSharedTargets/ReleaseFirst/DisableInputs/";
                var endings = new[] { "Complete", "Error", "Dispose", "ReleaseAll" };
                var expected = new List<string>();
                foreach (var ending in endings)
                {
                    expected.Add(prefix + ending);
                    expected.Add(prefix + "ResumePublication/" + ending);
                }

                Assert.That(report.Nodes.Where(node => endings.Contains(node.LabId)).Select(node => node.History),
                    Is.EquivalentTo(expected));
                TestContext.WriteLine($"When lifecycle: {expected.Count} terminal histories verified.");
            }
            finally
            {
                DisposeReplaySubjects(result);
            }
        }

        // UW1-UW8: Continue through repeated lifecycles with the same assertions as the bounded test.
        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task WhenSharingAndLifetimesContinueWithRandomSeed(int seed)
        {
            using var project = new EventStreamLifecycleProject<WhenModel>(
                DisposeCycle,
                ("", WhenProject.CreateLifecycleLabs));
            await RandomContinuousTest.RunAsync(project, seed);
        }

        private static void DisposeReplaySubjects(Node<WhenModel> node)
        {
            DisposeCycle(node.Model);

            foreach (var child in node.Afters) DisposeReplaySubjects(child);
        }

        private static void DisposeCycle(WhenModel model)
        {
            model.Branch?.Dispose();
            model.Source?.Dispose();
        }
    }
}
