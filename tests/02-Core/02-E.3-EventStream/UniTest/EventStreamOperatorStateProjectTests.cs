using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamOperatorStateProjectTests
    {
        // Internal
        private enum OperatorKind { Take, Skip, TakeWhile, SkipWhile }

        private class OperatorModel : Model
        {
            public EventStream<int> Source { get; private set; }
            public EventStream<int> Operator { get; private set; }

            private readonly List<int> _earlyValues = new();
            private readonly List<int> _lateValues = new();
            private readonly List<int> _sourceAfterEnd = new();
            private readonly List<Exception> _errors = new();
            private readonly Exception _failure = new ApplicationException("condition");
            private OperatorKind _kind;
            private Handle _first;
            private Handle _second;
            private int _earlyCompletions;
            private int _lateCompletions;
            private int _predicateCalls;
            private bool _failCondition;
            private Exception _observedFailure;
            private bool _activeAfterFirstRelease;
            private int _connectionsAfterFirstRelease;


            public void PrimeDirectInput(OperatorKind kind)
            {
                _kind = kind;
                Source = new EventStream<int>();
                Operator = kind switch
                {
                    OperatorKind.Take => Source.Take(3),
                    OperatorKind.Skip => Source.Skip(2),
                    OperatorKind.TakeWhile => Source.TakeWhile(CheckCondition),
                    _ => Source.SkipWhile(CheckCondition)
                };
                Subject = Operator;
                Source.OnNext(99);
                Operator.OnNext(1);
            }

            private bool CheckCondition(int value)
            {
                _predicateCalls++;
                if (_failCondition) throw _failure;

                return value < (_kind == OperatorKind.TakeWhile ? 4 : 3);
            }

            public void AssertPrimed()
            {
                Assert.That(Source.NodeCount, Is.Zero);
                Assert.That(Operator.IsDisposed, Is.False);
                Assert.That(Operator.NodeCount, Is.Zero);
                Assert.That(_predicateCalls, Is.EqualTo(IsConditional() ? 1 : 0));
                Assert.That(_earlyValues, Is.Empty);
                Assert.That(_lateValues, Is.Empty);
            }

            public void SubscribeAndAdvance()
            {
                _first = (Handle)Operator.Subscribe(_earlyValues.Add, () => _earlyCompletions++, _errors.Add);
                Source.OnNext(2);
            }

            public void DisableInputs()
            {
                Operator.Enabled = false;
                Source.OnNext(99);
                Operator.OnNext(99);
            }

            public void ResumeAndSubscribeLate()
            {
                Operator.Enabled = true;
                _second = (Handle)Operator.Subscribe(_lateValues.Add, () => _lateCompletions++, _errors.Add);
            }

            public void AssertBeforeLastValue(bool enabled, bool lateSubscribed)
            {
                AssertValues(IsTaking() ? new[] { 2 } : Array.Empty<int>(), Array.Empty<int>(), 2);
                Assert.That(Operator.Enabled, Is.EqualTo(enabled));
                Assert.That(Source.NodeCount, Is.EqualTo(1));
                Assert.That(_first.IsDisposed, Is.False);
                Assert.That(_second != null, Is.EqualTo(lateSubscribed));
                if (_second != null) Assert.That(_second.IsDisposed, Is.False);

                AssertNotifications(0);
            }

            public void AssertAfterPublication()
            {
                AssertValues(IsTaking() ? new[] { 2, 3 } : new[] { 3 }, new[] { 3 }, 3);
                if (_kind == OperatorKind.Take) AssertClosed(1);
                else
                {
                    Assert.That(Operator.IsDisposed, Is.False);
                    Assert.That(_first.IsDisposed || _second.IsDisposed, Is.False);
                    Assert.That(Source.NodeCount, Is.EqualTo(1));
                    AssertNotifications(0);
                }
            }

            public void AssertConditionClosed()
            {
                AssertValues(new[] { 2, 3 }, new[] { 3 }, 4);
                AssertClosed(1);
            }

            public void ReleaseLast(bool publishAfterOpen)
            {
                if (publishAfterOpen) Source.OnNext(1);

                _first.Dispose();
                _activeAfterFirstRelease = !Operator.IsDisposed && !_second.IsDisposed;
                _connectionsAfterFirstRelease = Source.NodeCount;
                _second.Dispose();
                using var surviving = Source.Subscribe(_sourceAfterEnd.Add);
                Source.OnNext(9);
            }

            public void AssertReleased(bool publishedAfterOpen)
            {
                var values = publishedAfterOpen ? new[] { 3, 1 } : new[] { 3 };
                AssertValues(values, values, 3);
                Assert.That(_activeAfterFirstRelease, Is.True);
                Assert.That(_connectionsAfterFirstRelease, Is.EqualTo(1));
                Assert.That(_sourceAfterEnd, Is.EqualTo(new[] { 9 }));
                AssertClosed(0);
            }

            public void FailCondition()
            {
                _failCondition = true;
                using var sibling = Source.Subscribe(_sourceAfterEnd.Add);
                try
                {
                    Source.OnNext(3);
                }
                catch (Exception exception)
                {
                    _observedFailure = exception;
                }

                Source.OnNext(4);
            }

            public void AssertConditionFailed()
            {
                Assert.That(_observedFailure, Is.SameAs(_failure));
                Assert.That(_sourceAfterEnd, Is.EqualTo(new[] { 3, 4 }));
                AssertValues(IsTaking() ? new[] { 2 } : Array.Empty<int>(), Array.Empty<int>(), 3);
                AssertClosed(0);
            }

            private bool IsTaking() => _kind == OperatorKind.Take || _kind == OperatorKind.TakeWhile;
            private bool IsConditional() => _kind == OperatorKind.TakeWhile || _kind == OperatorKind.SkipWhile;

            private void AssertValues(int[] early, int[] late, int predicateCalls)
            {
                Assert.That(_earlyValues, Is.EqualTo(early));
                Assert.That(_lateValues, Is.EqualTo(late));
                Assert.That(_predicateCalls, Is.EqualTo(IsConditional() ? predicateCalls : 0));
            }

            private void AssertNotifications(int completions)
            {
                Assert.That(_earlyCompletions, Is.EqualTo(completions));
                Assert.That(_lateCompletions, Is.EqualTo(completions));
                Assert.That(_errors, Is.Empty);
            }

            private void AssertClosed(int completions)
            {
                AssertNotifications(completions);
                Assert.That(Operator.IsDisposed && _first.IsDisposed && _second.IsDisposed, Is.True);
                Assert.That(Operator.NodeCount, Is.Zero);
                Assert.That(Source.NodeCount, Is.Zero);
                Assert.That(Source.IsDisposed, Is.False);
                Assert.Throws<ObjectDisposedException>(() => Operator.Subscribe(_ => { }));
            }
        }

        private class OperatorProject : CompactProject<OperatorModel>
        {
            private readonly OperatorKind _kind;

            public OperatorProject(OperatorKind kind) : base(ExecutionMode.Inline) => _kind = kind;

            public override IEnumerable<CompactLab<OperatorModel>> CreateCompactLabs(OperatorModel model) =>
                CreateLifecycleLabs(model.ExecutionCount, _kind);

            public static IEnumerable<CompactLab<OperatorModel>> CreateLifecycleLabs(int step, OperatorKind kind)
            {
                switch (step)
                {
                    case 0:
                        yield return new CompactLab<OperatorModel>(
                            "PrimeDirectInput",
                            actor: state => state.PrimeDirectInput(kind),
                            asserter: state => state.AssertPrimed());
                        break;
                    case 1:
                        yield return new CompactLab<OperatorModel>(
                            "SubscribeAndAdvance",
                            actor: state => state.SubscribeAndAdvance(),
                            asserter: state => state.AssertBeforeLastValue(true, false));
                        break;
                    case 2:
                        yield return new CompactLab<OperatorModel>(
                            "DisableInputs",
                            actor: state => state.DisableInputs(),
                            asserter: state => state.AssertBeforeLastValue(false, false));
                        break;
                    case 3:
                        yield return new CompactLab<OperatorModel>(
                            "ResumeAndSubscribeLate",
                            actor: state => state.ResumeAndSubscribeLate(),
                            asserter: state => state.AssertBeforeLastValue(true, true));
                        break;
                    case 4:
                        yield return new CompactLab<OperatorModel>(
                            "PublishRemaining",
                            actor: state => state.Source.OnNext(3),
                            asserter: state => state.AssertAfterPublication(),
                            toUncontinuable: kind == OperatorKind.Take);

                        if (kind == OperatorKind.TakeWhile || kind == OperatorKind.SkipWhile)
                        {
                            yield return new CompactLab<OperatorModel>(
                                "FailCondition",
                                actor: state => state.FailCondition(),
                                asserter: state => state.AssertConditionFailed(),
                                toUncontinuable: true);
                        }
                        break;
                    case 5:
                        if (kind == OperatorKind.TakeWhile)
                        {
                            yield return new CompactLab<OperatorModel>(
                                "CloseCondition",
                                actor: state => state.Source.OnNext(4),
                                asserter: state => state.AssertConditionClosed(),
                                toUncontinuable: true);
                        }
                        else
                        {
                            var publishAfterOpen = kind == OperatorKind.SkipWhile;
                            yield return new CompactLab<OperatorModel>(
                                publishAfterOpen ? "PublishAfterOpenAndRelease" : "ReleaseLast",
                                actor: state => state.ReleaseLast(publishAfterOpen),
                                asserter: state => state.AssertReleased(publishAfterOpen),
                                toUncontinuable: true);
                        }
                        break;
                }
            }
        }


        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        // US1-US6: Replay direct input, disabled input, late subscription, and normal or failed endings.
        [TestCase("Take")]
        [TestCase("Skip")]
        [TestCase("TakeWhile")]
        [TestCase("SkipWhile")]
        public async Task StatefulOperatorLifetimesSucceedThroughUniTest(string operatorName)
        {
            var kind = Enum.Parse<OperatorKind>(operatorName);
            var result = await new OperatorProject(kind).Execute(6, stopOnFailure: false);
            try
            {
                var report = result.GetReport();
                Assert.That(result.AllSucceed(out var cancelled), Is.True, report.ToJson());
                Assert.That(cancelled, Is.False);
                const string prefix = "PrimeDirectInput/SubscribeAndAdvance/DisableInputs/ResumeAndSubscribeLate/";
                var ending = kind switch
                {
                    OperatorKind.Take => "PublishRemaining",
                    OperatorKind.Skip => "ReleaseLast",
                    OperatorKind.TakeWhile => "CloseCondition",
                    _ => "PublishAfterOpenAndRelease"
                };
                var expected = new List<string>
                {
                    prefix + (kind == OperatorKind.Take ? "" : "PublishRemaining/") + ending
                };
                if (kind == OperatorKind.TakeWhile || kind == OperatorKind.SkipWhile)
                    expected.Add(prefix + "FailCondition");

                Assert.That(report.Nodes
                    .Where(node => node.LabId == ending || node.LabId == "FailCondition")
                    .Select(node => node.History), Is.EquivalentTo(expected));
            }
            finally
            {
                DisposeReplaySubjects(result);
            }
        }

        // US1-US6: Continue through repeated lifecycles with the same assertions as the bounded test.
        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task StatefulOperatorLifetimesContinueWithRandomSeed(int seed)
        {
            using var project = new EventStreamLifecycleProject<OperatorModel>(
                DisposeCycle,
                ("Take", step => OperatorProject.CreateLifecycleLabs(step, OperatorKind.Take)),
                ("Skip", step => OperatorProject.CreateLifecycleLabs(step, OperatorKind.Skip)),
                ("TakeWhile", step => OperatorProject.CreateLifecycleLabs(step, OperatorKind.TakeWhile)),
                ("SkipWhile", step => OperatorProject.CreateLifecycleLabs(step, OperatorKind.SkipWhile)));
            await RandomContinuousTest.RunAsync(project, seed);
        }

        private static void DisposeReplaySubjects(Node<OperatorModel> node)
        {
            DisposeCycle(node.Model);

            foreach (var child in node.Afters) DisposeReplaySubjects(child);
        }

        private static void DisposeCycle(OperatorModel model)
        {
            model.Operator?.Dispose();
            model.Source?.Dispose();
        }

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);
    }
}
