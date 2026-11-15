using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.UniEngine.StateMachines.Usm;
using BlackThunder.UniEngine.StateMachines.Usm.Core;
using NUnit.Framework;
using Parallel = BlackThunder.UniEngine.StateMachines.Usm.Parallel;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture]
    [Category("Usm")]
    public class UsmTtcTests
    {
        [TestCase(DetailedOperationStatus.NeverRun, OperationStatus.NeverRun)]
        [TestCase(DetailedOperationStatus.Running, OperationStatus.Running)]
        [TestCase(DetailedOperationStatus.Success, OperationStatus.Success)]
        [TestCase(DetailedOperationStatus.Failure, OperationStatus.Failure)]
        [TestCase(DetailedOperationStatus.ChildCompleted, OperationStatus.Success)]
        [TestCase(DetailedOperationStatus.ChildFailed, OperationStatus.Failure)]
        [TestCase(DetailedOperationStatus.HaltedBySelf, OperationStatus.Halted)]
        [TestCase(DetailedOperationStatus.Preempted, OperationStatus.Halted)]
        [TestCase(DetailedOperationStatus.HaltedByParent, OperationStatus.Halted)]
        [TestCase(DetailedOperationStatus.HaltedByExternal, OperationStatus.Halted)]
        public void OperationStatusIsDerived(DetailedOperationStatus detailed, OperationStatus status)
        {
            var info = new OperationInfo(detailed, 42);
            Assert.That(info.OperationStatus, Is.EqualTo(status));
            Assert.That(info.TryGetResult<int>(out var value), Is.True);
            Assert.That(value, Is.EqualTo(42));
            Assert.That(info.TryGetResult<string>(out _), Is.False);
            Assert.That(default(OperationInfo).TryGetResult<object>(out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new OperationInfo((DetailedOperationStatus)100));
        }

        [Test]
        public void ConstructionCopiesChildrenAndPreventsArrayMutationFromCreatingACycle()
        {
            var child = new Node("child");
            var children = new[] { child };
            var root = new Node("root", transitioner: new Sequence(), children: children);
            children[0] = root;
            root.Initialize();
            Assert.That(root.Children.Single(), Is.SameAs(child));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)root.Children)[0] = root);
            Assert.Throws<ArgumentNullException>(() => new Node(null));
            Assert.Throws<ArgumentException>(() => new Node("root", children: new Node[] { null }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Node("root", abortPolicies: (AbortPolicies)8));
            Assert.Throws<ArgumentOutOfRangeException>(() => root.ReframeOption = (ReframeOption)5);
        }

        [Test]
        public void ValidationRejectsMissingTransitionerDuplicateIdsAndSharedChildrenBeforeCallbacks()
        {
            var initializations = 0;
            var work = new UsmTestWork { Initializing = () => initializations++ };
            var missing = new Node("root", work, children: new[] { new Node("a") });
            Assert.Throws<InvalidOperationException>(() => missing.Initialize());
            Assert.Throws<InvalidOperationException>(() => missing.Initialize());
            var duplicate = new Node("root", work, new Sequence(), new[] { new Node("a"), new Node("a") });
            Assert.Throws<InvalidOperationException>(() => duplicate.Initialize());
            var shared = new Node("shared");
            var tree = new Node("root", work, new Sequence(), new[]
            {
                new Node("a", transitioner: new Sequence(), children: new[] { shared }),
                new Node("b", transitioner: new Sequence(), children: new[] { shared })
            });
            Assert.Throws<InvalidOperationException>(() => tree.Initialize());
            Assert.That(initializations, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WorkAndTransitionerCannotBeSharedWithinOrAcrossTrees(bool transitioner)
        {
            var work = new Work();
            var flow = new Sequence();
            Node Make(string id) => transitioner ? new Node(id, transitioner: flow) : new Node(id, work);
            var duplicate = new Node("root", transitioner: new Sequence(), children: new[] { Make("a"), Make("b") });
            Assert.Throws<InvalidOperationException>(() => duplicate.Initialize());
            var first = UsmTests.Ready(Make("first"));
            Assert.Throws<InvalidOperationException>(() => Make("second").Initialize());
            Assert.DoesNotThrow(() => first.Update(new FrameContext()));
        }

        [Test]
        public void InitializedChildrenCannotBeAttachedToAnotherRootAndBranchIdsMayRepeat()
        {
            var used = UsmTests.Ready(new Node("used"));
            var invalid = new Node("root", transitioner: new Sequence(), children: new[] { used });
            Assert.Throws<InvalidOperationException>(() => invalid.Initialize());
            var root = new Node("root", transitioner: new Sequence(), children: new[]
            {
                new Node("same"),
                new Node("branch", transitioner: new Selector(), children: new[] { new Node("same") })
            });
            Assert.DoesNotThrow(() => root.Initialize());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReframeConsumesRequestsAndPreservesRequestsRaisedDuringReplay(bool terminal)
        {
            var work = new UsmTestWork
            {
                Result = _ => new OperationInfo(terminal ? DetailedOperationStatus.Success : DetailedOperationStatus.Running)
            };
            var root = UsmTests.Ready(new Node("root", work));
            work.Updating = () =>
            {
                if (work.Updates < 3) root.ReframeOption = ReframeOption.Update;
            };
            var frame = new FrameContext("input");
            root.Update(frame);
            Assert.That(work.Updates, Is.EqualTo(3));
            Assert.That(frame.UpdateCount, Is.EqualTo(3));
            Assert.That(work.Starts, Is.EqualTo(terminal ? 3 : 1));
            Assert.That(work.Stops, Is.EqualTo(terminal ? 3 : 0));
            Assert.That(root.ReframeOption, Is.EqualTo(ReframeOption.None));
            root.Update(new FrameContext());
            Assert.That(work.Updates, Is.EqualTo(4));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReframeConditionUsesLatestOutputAndCanHaltCompletedOrRunningNode(bool terminal)
        {
            object observed = null;
            var work = new UsmTestWork
            {
                Output = _ => "output",
                Condition = input => { observed = input; return false; },
                Result = _ => new OperationInfo(terminal ? DetailedOperationStatus.Success : DetailedOperationStatus.Running)
            };
            var root = UsmTests.Ready(new Node("root", work));
            root.ReframeOption = ReframeOption.CheckConditionThenUpdate;
            var frame = new FrameContext("input");
            var result = root.Update(frame);
            Assert.That(observed, Is.EqualTo("output"));
            Assert.That(result.DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.HaltedBySelf));
            Assert.That(result.Result, Is.EqualTo("output"));
            Assert.That(work.Updates, Is.EqualTo(1));
            Assert.That(work.Stops, Is.EqualTo(1));
            Assert.That(frame.UpdateCount, Is.EqualTo(2));
            Assert.That(root.ReframeOption, Is.EqualTo(ReframeOption.None));
        }

        [Test]
        public void AcceptedReframeConditionRunsOnceAndExplicitStopCancelsReplay()
        {
            var checks = 0;
            var work = new UsmTestWork { Condition = _ => { checks++; return true; } };
            var root = UsmTests.Ready(new Node("root", work));
            root.ReframeOption = ReframeOption.CheckConditionThenUpdate;
            root.Update(new FrameContext());
            Assert.That(checks, Is.EqualTo(1));
            Assert.That(work.Updates, Is.EqualTo(2));
            work.Updating = () =>
            {
                root.ReframeOption = ReframeOption.Update;
                root.Stop(new FrameContext());
            };
            root.Update(new FrameContext());
            Assert.That(work.Updates, Is.EqualTo(3));
            Assert.That(root.ReframeOption, Is.EqualTo(ReframeOption.None));
            Assert.That(root.LastResult.DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.HaltedByExternal));
        }

        [Test]
        public void EndlessReframeSharesBudgetAndCleansUp()
        {
            var work = new UsmTestWork();
            var child = new Node("child", work);
            var root = UsmTests.Ready(new Node("root", transitioner: new Sequence(), children: new[] { child }));
            work.Updating = () => child.ReframeOption = ReframeOption.Update;
            var frame = new FrameContext(maxNodeUpdates: 5);
            Assert.Throws<InvalidOperationException>(() => root.Update(frame));
            Assert.That(frame.UpdateCount, Is.EqualTo(5));
            Assert.That(work.Updates, Is.EqualTo(4));
            Assert.That(work.Stops, Is.EqualTo(1));
            Assert.That(root.IsActive || child.IsActive, Is.False);
            Assert.That(child.ReframeOption, Is.EqualTo(ReframeOption.None));
        }

        [TestCase(false, LoopType.None, false)]
        [TestCase(false, LoopType.None, true)]
        [TestCase(false, LoopType.Conditional, false)]
        [TestCase(false, LoopType.Conditional, true)]
        [TestCase(false, LoopType.Forced, false)]
        [TestCase(false, LoopType.Forced, true)]
        [TestCase(true, LoopType.None, false)]
        [TestCase(true, LoopType.None, true)]
        [TestCase(true, LoopType.Conditional, false)]
        [TestCase(true, LoopType.Conditional, true)]
        [TestCase(true, LoopType.Forced, false)]
        [TestCase(true, LoopType.Forced, true)]
        public void EvaluatorLoopPoliciesApplyAtRoundBoundaries(bool sequence, LoopType loop, bool success)
        {
            var parent = new UsmTestWork();
            var child = new UsmTestWork
            {
                Result = _ => new OperationInfo(success ? DetailedOperationStatus.Success : DetailedOperationStatus.Failure)
            };
            ITransitioner flow = sequence ? new Sequence(loop) : new Selector(loop);
            var root = UsmTests.Ready(new Node("root", parent, flow,
                new[] { new Node("child", child, abortPolicies: AbortPolicies.StopOnFailure) }));
            var repeat = loop == LoopType.Forced || (loop == LoopType.Conditional && success);
            var expected = repeat ? DetailedOperationStatus.Running
                : success ? DetailedOperationStatus.ChildCompleted : DetailedOperationStatus.ChildFailed;
            Assert.That(root.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(expected));
            Assert.That(child.Updates, Is.EqualTo(1));
            Assert.That(root.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(expected));
            Assert.That(child.Updates, Is.EqualTo(2));
            Assert.That(parent.Starts, Is.EqualTo(repeat ? 1 : 2));
        }

        [TestCase(false, LoopType.None)]
        [TestCase(false, LoopType.Conditional)]
        [TestCase(false, LoopType.Forced)]
        [TestCase(true, LoopType.None)]
        [TestCase(true, LoopType.Conditional)]
        [TestCase(true, LoopType.Forced)]
        public void EmptyEvaluatorsApplyLoopPolicyToSuccessfulRound(bool sequence, LoopType loop)
        {
            ITransitioner flow = sequence ? new Sequence(loop) : new Selector(loop);
            var root = UsmTests.Ready(new Node("root", transitioner: flow));
            var frame = new FrameContext();
            var result = root.Update(frame);
            Assert.That(result.IsSuccess, Is.EqualTo(loop == LoopType.None));
            Assert.That(result.IsRunning, Is.EqualTo(loop != LoopType.None));
            Assert.That(frame.UpdateCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelectorFailureOnlyAdvancesWhenChildHasStopOnFailure(bool advance)
        {
            var a = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Failure) };
            var b = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success) };
            var root = UsmTests.Ready(new Node("root", transitioner: new Selector(), children: new[]
            {
                new Node("a", a, abortPolicies: advance ? AbortPolicies.StopOnFailure : AbortPolicies.None),
                new Node("b", b)
            }));
            for (var i = 0; i < 2; i++) Assert.That(root.Update(new FrameContext()).IsSuccess, Is.EqualTo(advance));
            Assert.That(a.Updates, Is.EqualTo(2));
            Assert.That(b.Updates, Is.EqualTo(advance ? 2 : 0));
        }

        [Test]
        public void DirectRequestsAreDeferredAndLastRequestWins()
        {
            var flow = new DirectTransitioner();
            var a = new UsmTestWork();
            var b = new UsmTestWork();
            var root = UsmTests.Ready(new Node("root", transitioner: flow,
                children: new[] { new Node("a", a), new Node("b", b) }));
            Assert.That(root.Update(new FrameContext()).IsRunning, Is.True);
            Assert.That(a.Starts + b.Starts, Is.Zero);
            flow.SetNext("a");
            flow.SetNext("b");
            Assert.That(b.Starts, Is.Zero);
            root.Update(new FrameContext());
            Assert.That(a.Starts, Is.Zero);
            Assert.That(b.Starts, Is.EqualTo(1));
            flow.SetNext("b");
            root.Update(new FrameContext());
            Assert.That(b.Starts, Is.EqualTo(1));
            Assert.That(b.Updates, Is.EqualTo(2));
            flow.SetNext(null);
            Assert.That(root.Update(new FrameContext()).IsSuccess, Is.True);
            Assert.That(b.Stops, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => flow.SetNext("unknown"));
        }

        [Test]
        public void DirectChildRequestsWaitForNextUpdateAndPreserveTerminalOutput()
        {
            var flow = new DirectTransitioner();
            flow.SetNext("a");
            var a = new UsmTestWork
            {
                Result = _ => new OperationInfo(DetailedOperationStatus.Success),
                Output = _ => "a output",
                Updating = () => flow.SetNext("b")
            };
            var b = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success) };
            var root = UsmTests.Ready(new Node("root", transitioner: flow,
                children: new[] { new Node("a", a), new Node("b", b) }));
            var first = root.Update(new FrameContext());
            Assert.That(first.IsRunning, Is.True);
            Assert.That(first.Result, Is.EqualTo("a output"));
            Assert.That(b.Starts, Is.Zero);
            Assert.That(root.Update(new FrameContext(first.Result)).IsSuccess, Is.True);
            Assert.That(b.Updates, Is.EqualTo(1));
        }

        [Test]
        public void DirectRejectsUnknownPreinitializedTargetAndMatrixGetNextDistinguishesNull()
        {
            var flow = new DirectTransitioner();
            flow.SetNext("unknown");
            Assert.Throws<InvalidOperationException>(() => new Node("root", transitioner: flow).Initialize());
            var matrix = new DecisionMatrixBuilder().FromEntry().With(null).GoTo(null).Build();
            Assert.That(matrix.GetNext(null, new OperationInfo(DetailedOperationStatus.Running), out var next), Is.True);
            Assert.That(next, Is.Null);
            Assert.That(matrix.GetNext(null, new OperationInfo(DetailedOperationStatus.Running, "x"), out _), Is.False);
        }

        [Test]
        public void ReframeCanRevisitMatrixSelfTransitionWithoutRecursiveUpdate()
        {
            var work = new UsmTestWork();
            var matrix = new DecisionMatrixBuilder().FromEntry().GoTo("child")
                .From("child").When(DetailedOperationStatus.Running).GoTo("child").Build();
            var root = UsmTests.Ready(new Node("root", transitioner: new MatrixTransitioner(matrix),
                children: new[] { new Node("child", work) }));
            root.ReframeOption = ReframeOption.Update;
            var frame = new FrameContext();
            root.Update(frame);
            Assert.That(work.Updates, Is.EqualTo(2));
            Assert.That(work.Starts, Is.EqualTo(1));
            Assert.That(frame.UpdateCount, Is.EqualTo(4));
        }

        [Test]
        public void ParallelPreservesCompletedChildrenAndReturnsIndependentSnapshots()
        {
            var a = new UsmTestWork
            {
                Result = _ => new OperationInfo(DetailedOperationStatus.Success), Output = value => value + "-a"
            };
            var b = new UsmTestWork { Output = value => value + "-b" };
            var root = UsmTests.Ready(new Node("root", transitioner: new Parallel(),
                children: new[] { new Node("a", a), new Node("b", b) }));
            var first = root.Update(new FrameContext("input"));
            Assert.That(first.TryGetResult<IReadOnlyList<OperationInfo>>(out var snapshot), Is.True);
            Assert.That(snapshot.Select(info => info.Result), Is.EqualTo(new[] { "input-a", "input-b" }));
            Assert.Throws<NotSupportedException>(() => ((IList<OperationInfo>)snapshot)[0] = default);
            b.Result = _ => new OperationInfo(DetailedOperationStatus.Success);
            var second = root.Update(new FrameContext("next"));
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(a.Updates, Is.EqualTo(1));
            Assert.That(b.Updates, Is.EqualTo(2));
            Assert.That(snapshot[1].IsRunning, Is.True);
            var completed = (IReadOnlyList<OperationInfo>)second.Result;
            Assert.That(completed[0].Result, Is.EqualTo("input-a"));
            Assert.That(completed[1].Result, Is.EqualTo("next-b"));
        }

        [TestCase(ParallelCompletionPolicy.AllSuccess)]
        [TestCase(ParallelCompletionPolicy.AnySuccess)]
        public void ParallelEarlyCompletionStopsActiveChildrenAndPreservesUnstartedChildren(
            ParallelCompletionPolicy policy)
        {
            var events = new List<string>();
            var a = new UsmTestWork("a", events) { Output = _ => "a result" };
            var b = new UsmTestWork("b", events)
            {
                Result = _ => new OperationInfo(policy == ParallelCompletionPolicy.AllSuccess
                    ? DetailedOperationStatus.Failure : DetailedOperationStatus.Success)
            };
            var c = new UsmTestWork("c", events);
            var parent = new UsmTestWork("parent", events);
            var root = UsmTests.Ready(new Node("root", parent, new Parallel(policy),
                new[] { new Node("a", a), new Node("b", b), new Node("c", c) }));
            var result = root.Update(new FrameContext("input"));
            var snapshot = (IReadOnlyList<OperationInfo>)result.Result;
            Assert.That(result.IsSuccess, Is.EqualTo(policy == ParallelCompletionPolicy.AnySuccess));
            Assert.That(snapshot[0].DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.HaltedByParent));
            Assert.That(snapshot[0].Result, Is.EqualTo("a result"));
            Assert.That(snapshot[2].DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.NeverRun));
            Assert.That(c.Starts, Is.Zero);
            Assert.That(events.IndexOf("a:stop:HaltedByParent"), Is.LessThan(events.Count - 1));
            Assert.That(parent.Stops, Is.EqualTo(1));
        }

        [TestCase(ParallelCompletionPolicy.AllSuccess)]
        [TestCase(ParallelCompletionPolicy.AnySuccess)]
        public void ParallelEmptyPolicyAndAllFailures(ParallelCompletionPolicy policy)
        {
            var empty = UsmTests.Ready(new Node("empty", transitioner: new Parallel(policy)));
            Assert.That(empty.Update(new FrameContext()).IsSuccess,
                Is.EqualTo(policy == ParallelCompletionPolicy.AllSuccess));
            var rejected = new UsmTestWork { Condition = _ => false };
            var failed = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Failure) };
            var root = UsmTests.Ready(new Node("root", transitioner: new Parallel(policy),
                children: new[] { new Node("reject", rejected), new Node("fail", failed) }));
            var result = root.Update(new FrameContext());
            Assert.That(result.IsFailure, Is.True);
            Assert.That(rejected.Starts, Is.Zero);
            Assert.That(failed.Starts, Is.EqualTo(policy == ParallelCompletionPolicy.AllSuccess ? 0 : 1));
        }

        [Test]
        public void ParallelSelfAbortDoesNotReexecuteCompletedSiblings()
        {
            var allowed = true;
            var a = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success) };
            var b = new UsmTestWork { Condition = _ => allowed };
            var root = UsmTests.Ready(new Node("root", transitioner: new Parallel(), children: new[]
            {
                new Node("a", a), new Node("b", b, abortPolicies: AbortPolicies.Self)
            }));
            root.Update(new FrameContext());
            allowed = false;
            var result = root.Update(new FrameContext());
            Assert.That(result.IsFailure, Is.True);
            Assert.That(a.Updates, Is.EqualTo(1));
            Assert.That(b.Updates, Is.EqualTo(1));
            Assert.That(b.StopReason, Is.EqualTo(DetailedOperationStatus.HaltedBySelf));
        }

        [Test]
        public void ParallelCleanupContinuesAfterAChildStopThrows()
        {
            var failure = new ApplicationException("cleanup");
            var a = new UsmTestWork { Stopping = () => throw failure };
            var b = new UsmTestWork();
            var winner = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success) };
            var root = UsmTests.Ready(new Node("root", transitioner: new Parallel(ParallelCompletionPolicy.AnySuccess),
                children: new[] { new Node("a", a), new Node("b", b), new Node("winner", winner) }));
            Assert.That(Assert.Throws<ApplicationException>(() => root.Update(new FrameContext())), Is.SameAs(failure));
            Assert.That(a.Stops, Is.EqualTo(1));
            Assert.That(b.Stops, Is.EqualTo(1));
            Assert.That(root.IsActive, Is.False);
        }


        [Test]
        public void ExplicitStopDiscardsReframeRequestsRaisedByStopCallbacks()
        {
            var work = new UsmTestWork();
            var root = UsmTests.Ready(new Node("root", work));
            work.Stopping = () => root.ReframeOption = ReframeOption.Update;
            root.Update(new FrameContext());
            root.Stop(new FrameContext());
            Assert.That(root.ReframeOption, Is.EqualTo(ReframeOption.None));
            work.Updating = () =>
            {
                root.Stop(new FrameContext());
                root.ReframeOption = ReframeOption.Update;
            };
            root.Update(new FrameContext());
            Assert.That(work.Updates, Is.EqualTo(2));
            Assert.That(root.ReframeOption, Is.EqualTo(ReframeOption.None));
        }

        [Test]
        public void RepeatedInactiveStopDoesNotConsumeAFutureReframeRequest()
        {
            var work = new UsmTestWork();
            var root = UsmTests.Ready(new Node("root", work));
            root.Update(new FrameContext());
            root.Stop(new FrameContext());
            root.ReframeOption = ReframeOption.Update;
            root.Stop(new FrameContext());
            Assert.That(root.ReframeOption, Is.EqualTo(ReframeOption.Update));
            root.Update(new FrameContext());
            Assert.That(work.Updates, Is.EqualTo(3));
            Assert.That(work.Stops, Is.EqualTo(1));
        }

        [Test]
        public void StopOnFailureDoesNotTreatExternalHaltAsFailure()
        {
            var work = new UsmTestWork();
            var child = new Node("a", work, abortPolicies: AbortPolicies.StopOnFailure);
            var fallback = new UsmTestWork();
            var root = UsmTests.Ready(new Node("root", transitioner: new Selector(),
                children: new[] { child, new Node("b", fallback) }));
            work.Updating = () => child.Stop(new FrameContext());
            Assert.That(root.Update(new FrameContext()).IsRunning, Is.True);
            Assert.That(fallback.Starts, Is.Zero);
        }

        [Test]
        public void DirectReplacementStopsRunningChildAndCompletedSelfRequestRestartsNextUpdate()
        {
            var flow = new DirectTransitioner();
            var a = new UsmTestWork();
            var b = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success) };
            var root = UsmTests.Ready(new Node("root", transitioner: flow,
                children: new[] { new Node("a", a), new Node("b", b) }));
            flow.SetNext("a");
            root.Update(new FrameContext());
            flow.SetNext("b");
            b.Updating = () => flow.SetNext("b");
            root.Update(new FrameContext());
            Assert.That(a.StopReason, Is.EqualTo(DetailedOperationStatus.Preempted));
            Assert.That(b.Updates, Is.EqualTo(1));
            root.Update(new FrameContext());
            Assert.That(b.Starts, Is.EqualTo(2));
            Assert.That(b.Stops, Is.EqualTo(2));
            Assert.That(root.IsActive, Is.True);
        }

        [Test]
        public void ExceptionReportsFailureAndCancelsReframe()
        {
            var work = new UsmTestWork { Updating = () => throw new ApplicationException("run") };
            var root = UsmTests.Ready(new Node("root", work));
            root.ReframeOption = ReframeOption.Update;
            var frame = new FrameContext();
            Assert.Throws<ApplicationException>(() => root.Update(frame));
            Assert.That(root.LastResult.IsFailure, Is.True);
            Assert.That(frame.Reports.Any(report => report.OperationInfo.IsFailure), Is.True);
            Assert.That(root.ReframeOption, Is.EqualTo(ReframeOption.None));
        }


        [Test]
        public void MixedMatrixSequenceAndParallelRetainNestedResults()
        {
            var matrix = new DecisionMatrixBuilder().FromEntry().GoTo("sequence").Build();
            var leaf = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success) };
            var root = UsmTests.Ready(new Node("root", transitioner: new MatrixTransitioner(matrix), children: new[]
            {
                new Node("sequence", transitioner: new Sequence(), children: new[]
                {
                    new Node("parallel", transitioner: new Parallel(), children: new[] { new Node("leaf", leaf) })
                })
            }));
            var frame = new FrameContext("payload");
            var result = root.Update(frame);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(((IReadOnlyList<OperationInfo>)result.Result)[0].Result, Is.EqualTo("payload"));
            Assert.That(frame.UpdateCount, Is.EqualTo(4));
            Assert.That(frame.Nexts[0].Nexts[0].Nexts[0].Id, Is.EqualTo("leaf"));
        }
    }
}

