using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.UniEngine.StateMachines.Usm;
using BlackThunder.UniEngine.StateMachines.Usm.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture]
    [Category("Usm")]
    public class UsmTransitionTests
    {
        [Test]
        public void DecisionMatrixPrioritizesExplicitResultsAndPayloadMatches()
        {
            var matrix = new DecisionMatrixBuilder()
                .FromEntry().GoTo("default").With(null).GoTo("null")
                .From("a").When(_ => true).GoTo("predicate")
                .When(DetailedOperationStatus.Success).GoTo("complete").With("value").GoTo("exact")
                .Build();
            Assert.That(matrix.TryGetEntry(null, out var next), Is.True);
            Assert.That(next, Is.EqualTo("null"));
            Assert.That(matrix.TryGetEntry(out next), Is.True);
            Assert.That(next, Is.EqualTo("default"));
            matrix.TryGetTransition("a", DetailedOperationStatus.Success, "value", out next);
            Assert.That(next, Is.EqualTo("exact"));
            matrix.TryGetTransition("a", DetailedOperationStatus.Success, "other", out next);
            Assert.That(next, Is.EqualTo("complete"));
            matrix.TryGetTransition("a", DetailedOperationStatus.Failure, out next);
            Assert.That(next, Is.EqualTo("predicate"));
        }

        [Test]
        public void DecisionMatrixSeparatesMissingRulesFromExplicitNullDestinations()
        {
            var empty = new DecisionMatrixBuilder().Build();
            Assert.That(empty.TryGetEntry(out _), Is.False);
            Assert.That(empty.TryGetTransition("a", DetailedOperationStatus.Success, out _), Is.False);
            var matrix = new DecisionMatrixBuilder().FromEntry().GoTo(null)
                .From("a").When(DetailedOperationStatus.Success).GoTo(null).Build();
            Assert.That(matrix.TryGetEntry(out var entry), Is.True);
            Assert.That(entry, Is.Null);
            Assert.That(matrix.TryGetTransition("a", DetailedOperationStatus.Success, out var next), Is.True);
            Assert.That(next, Is.Null);
        }

        [Test]
        public void PredicatePayloadRulesShareTheirDefaultAndKeepRegistrationOrder()
        {
            Func<DetailedOperationStatus, bool> first = _ => true;
            var matrix = new DecisionMatrixBuilder().From("a").When(first).GoTo("default")
                .With(null).GoTo(null).With("x").GoTo("specific")
                .When(_ => true).GoTo("later").Build();
            matrix.TryGetTransition("a", DetailedOperationStatus.Failure, "x", out var next);
            Assert.That(next, Is.EqualTo("specific"));
            matrix.TryGetTransition("a", DetailedOperationStatus.Failure, null, out next);
            Assert.That(next, Is.Null);
            matrix.TryGetTransition("a", DetailedOperationStatus.Failure, "other", out next);
            Assert.That(next, Is.EqualTo("default"));
        }

        [Test]
        public void MatrixBuilderCannotMutateBuiltMatrixThroughSavedSubBuilders()
        {
            var builder = new DecisionMatrixBuilder();
            var entry = builder.FromEntry();
            var when = builder.From("a").When(DetailedOperationStatus.Success);
            entry.GoTo("a");
            var matrix = builder.Build();
            Assert.Throws<InvalidOperationException>(() => entry.GoTo("b"));
            Assert.Throws<InvalidOperationException>(() => when.GoTo("b"));
            Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.That(matrix.TryGetEntry(out var next), Is.True);
            Assert.That(next, Is.EqualTo("a"));
            Assert.Throws<ArgumentException>(() => new DecisionMatrixBuilder().From(" "));
            Assert.Throws<ArgumentNullException>(() => new DecisionMatrixBuilder().From("a").When(null));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UnknownMatrixSourcesAndDestinationsFailInitialization(bool unknownSource)
        {
            var builder = new DecisionMatrixBuilder();
            if (unknownSource) builder.From("missing").When(DetailedOperationStatus.Success).GoTo("a");
            else builder.FromEntry().GoTo("missing");
            var root = new Node(
                "root",
                transitioner: new MatrixTransitioner(builder.Build()),
                children: new[]
                {
                    new Node("a")
                });
            Assert.Throws<InvalidOperationException>(() => root.Initialize());
            Assert.Throws<InvalidOperationException>(() => root.Update(new FrameContext()));
        }

        [Test]
        public void NestedMatrixAndSequencePassPayloadThroughOneFrame()
        {
            var first = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success), Output = input => input + "-a" };
            var second = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success), Output = input => input + "-b" };
            var finish = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success), Output = input => input + "-end" };
            var matrix = new DecisionMatrixBuilder().FromEntry().GoTo("stage")
                .From("stage").When(DetailedOperationStatus.ChildCompleted).With("input-a-b").GoTo("finish").Build();
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: new MatrixTransitioner(matrix),
                children: new[]
                {
                    new Node(
                        "stage",
                        transitioner: new Sequence(),
                        children: new[]
                        {
                            new Node("a", work: first, abortPolicies: AbortPolicies.StopOnFailure),
                            new Node("b", work: second)
                        }),
                    new Node("finish", work: finish)
                }));
            var context = new FrameContext("input");
            Assert.That(root.Update(context).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.Success));
            Assert.That(context.Payload, Is.EqualTo("input-a-b-end"));
            Assert.That(context.Nexts.Select(next => next.Id), Is.EqualTo(new[] { "stage", "finish" }));
            Assert.That(context.Nexts[0].Nexts.Select(next => next.Id), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(first.Stops + second.Stops + finish.Stops, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelfTransitionYieldsUntilNextUpdate(bool complete)
        {
            var work = new UsmTestWork { Result = _ => complete ? new OperationInfo(DetailedOperationStatus.Success) : new OperationInfo(DetailedOperationStatus.Running) };
            var matrix = new DecisionMatrixBuilder().FromEntry().GoTo("a")
                .From("a").When(_ => true).GoTo("a").Build();
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: new MatrixTransitioner(matrix),
                children: new[]
                {
                    new Node("a", work: work)
                }));
            root.Update(new FrameContext());
            Assert.That(work.Updates, Is.EqualTo(1));
            root.Update(new FrameContext());
            Assert.That(work.Updates, Is.EqualTo(2));
            Assert.That(work.Starts, Is.EqualTo(complete ? 2 : 1));
            Assert.That(root.IsActive, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CircularTransitionsShareBudgetIncludingRejectedConditions(bool rejectConditions)
        {
            var a = new UsmTestWork { Condition = _ => !rejectConditions };
            var b = new UsmTestWork { Condition = _ => !rejectConditions };
            var matrix = new DecisionMatrixBuilder().FromEntry().GoTo("a")
                .From("a").When(_ => true).GoTo("b").From("b").When(_ => true).GoTo("a").Build();
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: new MatrixTransitioner(matrix),
                children: new[]
                {
                    new Node("a", work: a, abortPolicies: AbortPolicies.Self),
                    new Node("b", work: b, abortPolicies: AbortPolicies.Self)
                }));
            var frame = new FrameContext(maxNodeUpdates: 7);
            Assert.Throws<InvalidOperationException>(() => root.Update(frame));
            Assert.That(frame.UpdateCount, Is.EqualTo(7));
            Assert.That(root.IsActive, Is.False);
            Assert.That(a.Starts + b.Starts, Is.EqualTo(a.Stops + b.Stops));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StopOnFailureDoesNotOverrideMatrixRecovery(bool stopOnFailure)
        {
            var failed = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Failure) };
            var recovery = new UsmTestWork();
            var matrix = new DecisionMatrixBuilder().FromEntry().GoTo("failed")
                .From("failed").When(DetailedOperationStatus.Failure).GoTo("recovery").Build();
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: new MatrixTransitioner(matrix),
                children: new[]
                {
                    new Node(
                        "failed",
                        work: failed,
                        abortPolicies: stopOnFailure ? AbortPolicies.StopOnFailure : AbortPolicies.None),
                    new Node("recovery", work: recovery)
                }));
            var result = root.Update(new FrameContext());
            Assert.That(result.DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.Running));
            Assert.That(recovery.Starts, Is.EqualTo(1));
        }

        [Test]
        public void EntryAbsenceKeepsParentRunningAndExplicitExitCompletesIt()
        {
            var matrix = new DecisionMatrixBuilder().FromEntry().With("exit").GoTo(null).Build();
            var root = UsmTests.Ready(new Node("root", transitioner: new MatrixTransitioner(matrix)));
            Assert.That(root.Update(new FrameContext("wait")).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.Running));
            Assert.That(root.Update(new FrameContext("exit")).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.ChildCompleted));
        }

        [Test]
        public void StopInsideMatrixPredicateDoesNotEnterItsDestination()
        {
            INode root = null;
            var later = new UsmTestWork();
            var matrix = new DecisionMatrixBuilder().FromEntry().GoTo("a")
                .From("a").When(_ => { root.Stop(new FrameContext()); return true; }).GoTo("b").Build();
            root = UsmTests.Ready(new Node(
                "root",
                transitioner: new MatrixTransitioner(matrix),
                children: new[]
                {
                    new Node("a"),
                    new Node("b", work: later)
                }));
            Assert.That(root.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.HaltedByExternal));
            Assert.That(later.Starts, Is.Zero);
        }

        [Test]
        public void SelectorFallsBackOnFailureAndCompletesOnSuccess()
        {
            var reject = new UsmTestWork { Condition = _ => false };
            var fail = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Failure) };
            var success = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success) };
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: new Selector(),
                children: new[]
                {
                    new Node("reject", work: reject),
                    new Node("fail", work: fail, abortPolicies: AbortPolicies.StopOnFailure),
                    new Node("success", work: success)
                }));
            Assert.That(root.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.ChildCompleted));
            Assert.That(reject.Starts, Is.Zero);
            Assert.That(fail.Stops, Is.EqualTo(1));
            Assert.That(success.Stops, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelectorOnlyPreemptsWhenHigherPriorityChildOptsIn(bool preempt)
        {
            var ready = false;
            var high = new UsmTestWork { Condition = _ => ready };
            var low = new UsmTestWork();
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: new Selector(),
                children: new[]
                {
                    new Node(
                        "high",
                        work: high,
                        abortPolicies: preempt ? AbortPolicies.LowerPriority : AbortPolicies.None),
                    new Node("low", work: low)
                }));
            root.Update(new FrameContext());
            ready = true;
            root.Update(new FrameContext());
            Assert.That(high.Starts, Is.EqualTo(preempt ? 1 : 0));
            Assert.That(low.Stops, Is.EqualTo(preempt ? 1 : 0));
            if (preempt) Assert.That(low.StopReason, Is.EqualTo(DetailedOperationStatus.Preempted));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelfConditionFailureFallsBackInSelectorAndStopsSequence(bool sequence)
        {
            var ready = true;
            var first = new UsmTestWork { Condition = _ => ready };
            var second = new UsmTestWork();
            ITransitioner transitioner = sequence ? new Sequence() : new Selector();
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: transitioner,
                children: new[]
                {
                    new Node("a", work: first, abortPolicies: AbortPolicies.Self),
                    new Node("b", work: second)
                }));
            root.Update(new FrameContext());
            ready = false;
            var result = root.Update(new FrameContext());
            Assert.That(first.StopReason, Is.EqualTo(DetailedOperationStatus.HaltedBySelf));
            Assert.That(second.Starts, Is.EqualTo(sequence ? 0 : 1));
            Assert.That(result.DetailedOperationStatus,
                Is.EqualTo(sequence ? DetailedOperationStatus.ChildFailed : DetailedOperationStatus.Running));
        }

        [Test]
        public void SequenceResumesRunningChildAndRestartsFromFirstAfterCompletion()
        {
            var first = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success) };
            var second = new UsmTestWork();
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: new Sequence(),
                children: new[]
                {
                    new Node("a", work: first, abortPolicies: AbortPolicies.StopOnFailure),
                    new Node("b", work: second)
                }));
            root.Update(new FrameContext());
            root.Update(new FrameContext());
            Assert.That(first.Updates, Is.EqualTo(1));
            Assert.That(second.Updates, Is.EqualTo(2));
            second.Result = _ => new OperationInfo(DetailedOperationStatus.Success);
            Assert.That(root.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.ChildCompleted));
            Assert.That(root.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.ChildCompleted));
            Assert.That(first.Starts, Is.EqualTo(2));
        }

        [Test]
        public void SequenceCanPreemptBackToEarlierChild()
        {
            var first = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Success) };
            var second = new UsmTestWork();
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: new Sequence(),
                children: new[]
                {
                    new Node("a", work: first, abortPolicies: AbortPolicies.LowerPriority),
                    new Node("b", work: second)
                }));
            root.Update(new FrameContext());
            first.Result = _ => new OperationInfo(DetailedOperationStatus.Running);
            root.Update(new FrameContext());
            Assert.That(first.Starts, Is.EqualTo(2));
            Assert.That(second.StopReason, Is.EqualTo(DetailedOperationStatus.Preempted));
            Assert.That(second.Updates, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyEvaluatorsComplete(bool sequence)
        {
            ITransitioner content = sequence ? new Sequence() : new Selector();
            var root = UsmTests.Ready(new Node("root", transitioner: content));
            Assert.That(root.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.ChildCompleted));
        }

        [Test]
        public void ReusingAChildFrameRefreshesPayload()
        {
            var frame = new FrameContext("first");
            var first = frame.GetOrAddNext("a", "first");
            var second = frame.GetOrAddNext("a", "second");
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Payload, Is.EqualTo("second"));
            Assert.That(frame.Nexts.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitNullTransitionStopsRunningChildAndCompletesParent()
        {
            var work = new UsmTestWork();
            var matrix = new DecisionMatrixBuilder().FromEntry().GoTo("a")
                .From("a").When(DetailedOperationStatus.Running).GoTo(null).Build();
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: new MatrixTransitioner(matrix),
                children: new[]
                {
                    new Node("a", work: work)
                }));
            Assert.That(root.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.ChildCompleted));
            Assert.That(work.Stops, Is.EqualTo(1));
            Assert.That(work.StopReason, Is.EqualTo(DetailedOperationStatus.Preempted));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedSequenceAndExhaustedSelectorDoNotEnterLaterWork(bool sequence)
        {
            var first = new UsmTestWork { Result = _ => new OperationInfo(DetailedOperationStatus.Failure) };
            var second = new UsmTestWork { Condition = _ => false };
            ITransitioner content = sequence ? new Sequence() : new Selector();
            var root = UsmTests.Ready(new Node(
                "root",
                transitioner: content,
                children: new[]
                {
                    new Node("a", work: first, abortPolicies: AbortPolicies.StopOnFailure),
                    new Node("b", work: second)
                }));
            Assert.That(root.Update(new FrameContext()).DetailedOperationStatus, Is.EqualTo(DetailedOperationStatus.ChildFailed));
            Assert.That(second.Starts, Is.Zero);
            Assert.That(root.IsActive, Is.False);
        }
    }
}
