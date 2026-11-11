using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamFlagsStateProjectTests
    {
        // Internal
        [Flags]
        private enum GeoEvents { Shape = 1, Location = 2 }

        [Flags]
        private enum RouteEvents : byte { Shape = 1 }

        [Flags]
        private enum WideEvents : ulong { Top = 1UL << 63 }

        private enum Phase { Update, Render }

        private class FlagsModel : Model
        {
            public EventStream<ulong> Source { get; private set; }
            public EventStream<ulong> Branch { get; private set; }

            private readonly List<string> _deliveries = new();
            private readonly List<string> _completions = new();
            private readonly List<Exception> _errors = new();
            private readonly List<ulong> _afterRelease = new();
            private readonly Exception _failure = new ApplicationException("flags source");
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
            public void RegisterSharedMasks()
            {
                Source = new EventStream<ulong>();
                Branch = Source.ForFlags(GeoEvents.Shape).Branch();
                Subject = Branch;
                _render = Observe(Branch.ForOrder(Phase.Render).ForFlags(RouteEvents.Shape), "render");
                _unmatched = Observe(Branch.ForOrder(Phase.Render).ForFlags(WideEvents.Top), "unmatched");
                _first = Observe(Branch.ForOrder(Phase.Update).ForFlags(GeoEvents.Shape), "first");
                _alias = Observe(Branch.ForOrder(Phase.Update).ForFlags(RouteEvents.Shape), "alias");
                Source.OnNext(0);
                Source.OnNext(GeoEvents.Location.ToBits());
                Source.OnNext(GeoEvents.Shape.ToBits());
            }

            private Handle Observe(EventPath<ulong> path, string name) =>
                (Handle)path.Subscribe(
                    value => _deliveries.Add($"{name}:{value}"),
                    () => _completions.Add(name),
                    _errors.Add);

            public void ReleaseFirst()
            {
                _first.Dispose();
                Source.OnNext((GeoEvents.Shape | GeoEvents.Location).ToBits());
            }

            public void DisableInputs()
            {
                Branch.Enabled = false;
                Source.OnNext(GeoEvents.Shape.ToBits());
                Branch.OnNext(GeoEvents.Shape.ToBits());
            }

            public void ResumePublication()
            {
                Branch.Enabled = true;
                _resumed = true;
                Source.OnNext(GeoEvents.Shape.ToBits());
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
                var expected = new List<string> { "first:1", "alias:1", "render:1" };
                if (firstReleased) expected.AddRange(new[] { "alias:3", "render:3" });
                if (_resumed) expected.AddRange(new[] { "alias:1", "render:1" });

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

                        using (Source.ForFlags(RouteEvents.Shape).Subscribe(_afterRelease.Add))
                        {
                            _freshNodes = Source.NodeCount;
                            Source.OnNext(GeoEvents.Location.ToBits());
                            Source.OnNext(GeoEvents.Shape.ToBits());
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
                Assert.Throws<ObjectDisposedException>(() => Branch.ForFlags(GeoEvents.Shape).Subscribe(_ => { }));

                if (ending == "ReleaseAll")
                {
                    Assert.That(_aliveAfterGroupRelease && _aliveWithUnmatchedOnly, Is.True);
                    Assert.That(_nodesAfterGroupRelease, Is.EqualTo(2));
                    Assert.That(_nodesWithUnmatchedOnly, Is.EqualTo(2));
                    Assert.That(_freshNodes, Is.EqualTo(1));
                    Assert.That(_afterRelease, Is.EqualTo(new[] { 1UL }));
                }
                else
                {
                    Assert.That(_afterRelease, Is.Empty);
                    Assert.Throws<ObjectDisposedException>(() => Source.OnNext(1));
                }
            }
        }

        private class FlagsProject : CompactProject<FlagsModel>
        {
            public FlagsProject() : base(ExecutionMode.Inline) { }

            public override IEnumerable<CompactLab<FlagsModel>> CreateCompactLabs(FlagsModel model) =>
                CreateLifecycleLabs(model.ExecutionCount);

            public static IEnumerable<CompactLab<FlagsModel>> CreateLifecycleLabs(int step)
            {
                switch (step)
                {
                    case 0:
                        yield return new CompactLab<FlagsModel>(
                            "RegisterSharedMasks",
                            actor: state => state.RegisterSharedMasks(),
                            asserter: state => state.AssertActive(false, true));
                        break;
                    case 1:
                        yield return new CompactLab<FlagsModel>(
                            "ReleaseFirst",
                            actor: state => state.ReleaseFirst(),
                            asserter: state => state.AssertActive(true, true));
                        break;
                    case 2:
                        yield return new CompactLab<FlagsModel>(
                            "DisableInputs",
                            actor: state => state.DisableInputs(),
                            asserter: state => state.AssertActive(true, false));
                        break;
                    case 3:
                        yield return new CompactLab<FlagsModel>(
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

            private static IEnumerable<CompactLab<FlagsModel>> CreateEndings()
            {
                foreach (var ending in new[] { "Complete", "Error", "Dispose", "ReleaseAll" })
                {
                    yield return new CompactLab<FlagsModel>(
                        ending,
                        actor: state => state.End(ending),
                        asserter: state => state.AssertEnded(ending),
                        toUncontinuable: true);
                }
            }
        }


        // Content
        // UF1-UF8: Replay shared masks, group isolation, disabled input, and every ending before or after resuming.
        [Test]
        public async Task FlagsSharingAndLifetimesSucceedThroughUniTest()
        {
            var result = await new FlagsProject().Execute(5, stopOnFailure: false);
            try
            {
                var report = result.GetReport();
                Assert.That(result.AllSucceed(out var cancelled), Is.True, report.ToJson());
                Assert.That(cancelled, Is.False);
                const string prefix = "RegisterSharedMasks/ReleaseFirst/DisableInputs/";
                var endings = new[] { "Complete", "Error", "Dispose", "ReleaseAll" };
                var expected = new List<string>();
                foreach (var ending in endings)
                {
                    expected.Add(prefix + ending);
                    expected.Add(prefix + "ResumePublication/" + ending);
                }

                Assert.That(report.Nodes.Where(node => endings.Contains(node.LabId)).Select(node => node.History),
                    Is.EquivalentTo(expected));
                TestContext.WriteLine($"ForFlags lifecycle: {expected.Count} terminal histories verified.");
            }
            finally
            {
                DisposeReplaySubjects(result);
            }
        }

        // UF1-UF8: Continue through repeated lifecycles with the same assertions as the bounded test.
        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task FlagsSharingAndLifetimesContinueWithRandomSeed(int seed)
        {
            using var project = new EventStreamLifecycleProject<FlagsModel>(
                DisposeCycle,
                ("", FlagsProject.CreateLifecycleLabs));
            await RandomContinuousTest.RunAsync(project, seed);
        }

        private static void DisposeReplaySubjects(Node<FlagsModel> node)
        {
            DisposeCycle(node.Model);

            foreach (var child in node.Afters) DisposeReplaySubjects(child);
        }

        private static void DisposeCycle(FlagsModel model)
        {
            model.Branch?.Dispose();
            model.Source?.Dispose();
        }
    }
}
