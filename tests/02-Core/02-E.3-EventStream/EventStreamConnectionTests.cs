using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamConnectionTests
    {
        // Content --------------------
        // Connection ownership
        // C1: A relation stays connected until the caller releases its last subscription.
        [Test]
        public void RelationStaysConnectedUntilItsLastSubscriptionIsReleased()
        {
            using var source = new EventStream<int>();
            using var branch = source.Branch();
            var values = new List<int>();
            var first = branch.Subscribe(values.Add);
            var second = branch.Subscribe(_ => { });

            source.OnNext(1);
            first.Dispose();
            Assert.That(source.NodeCount, Is.EqualTo(1));
            Assert.That(branch.IsDisposed, Is.False);
            second.Dispose();

            Assert.That(values, Is.EqualTo(new[] { 1 }));
            Assert.That(source.NodeCount, Is.Zero);
            Assert.That(branch.IsDisposed, Is.True);
            Assert.That(source.IsDisposed, Is.False);
        }

        // Termination
        // C4: Disabling values leaves completion and error notifications active.
        [TestCase(false)]
        [TestCase(true)]
        public void DisabledStreamStillForwardsCompletionOrErrorToItsRelation(bool sendError)
        {
            using var source = new EventStream<int>();
            using var branch = source.Branch();
            var values = new List<int>();
            var completed = 0;
            var errors = new List<Exception>();
            using var listener = branch.Subscribe(values.Add, () => completed++, errors.Add);
            var failure = new ApplicationException("source");

            source.OnNext(1);
            source.Enabled = false;
            source.OnNext(2);
            if (sendError) source.OnError(failure);
            else source.OnCompleted();

            Assert.That(values, Is.EqualTo(new[] { 1 }));
            Assert.That(completed, Is.EqualTo(sendError ? 0 : 1));
            Assert.That(errors, Is.EqualTo(sendError ? new Exception[] { failure } : Array.Empty<Exception>()));
            Assert.That(source.IsDisposed, Is.True);
            Assert.That(branch.IsDisposed, Is.True);
        }

        // C5: Explicit upstream disposal ends a relation silently, while an independent stream stays usable.
        [Test]
        public void DisposingAnUpstreamSilentlyReleasesItsRelation()
        {
            using var source = new EventStream<int>();
            using var branch = source.Branch();
            var notifications = 0;
            using var listener = branch.Subscribe(_ => { }, () => notifications++, _ => notifications++);

            source.Dispose();

            Assert.That(branch.IsDisposed, Is.True);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void DisposingAnUpstreamLeavesAStreamBuiltOnItUsable()
        {
            using var source = new EventStream<int>();
            using var child = new EventStream<int>(source);
            var values = new List<int>();
            var notifications = 0;
            using var listener = child.Subscribe(values.Add, () => notifications++, _ => notifications++);

            source.OnNext(1);
            source.Dispose();
            Assert.That(child.IsDisposed, Is.False);
            child.OnNext(2);

            Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(notifications, Is.Zero);
        }

        // Direct input
        // C6: Values published into a relation use the same path as upstream values and stay below it.
        [Test]
        public void DirectInputReachesOnlyTheBranch()
        {
            using var source = new EventStream<int>();
            using var branch = source.Branch();
            var sourceValues = new List<int>();
            var branchValues = new List<int>();
            using var original = source.Subscribe(sourceValues.Add);
            using var selected = branch.Subscribe(branchValues.Add);

            source.OnNext(1);
            branch.OnNext(2);

            Assert.That(sourceValues, Is.EqualTo(new[] { 1 }));
            Assert.That(branchValues, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void DirectInputIsCountedByTake()
        {
            using var source = new EventStream<int>();
            using var taken = source.Take(2);
            var values = new List<int>();
            var completions = 0;
            using var listener = taken.Subscribe(values.Add, () => completions++, null);

            taken.OnNext(1);
            source.OnNext(2);
            source.OnNext(3);

            Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(taken.IsDisposed, Is.True);
        }

        [Test]
        public void DisabledTakeDoesNotCountDirectInput()
        {
            using var taken = new EventStream<int>().Take(1);
            var values = new List<int>();
            using var listener = taken.Subscribe(values.Add);

            taken.Enabled = false;
            taken.OnNext(1);
            taken.Enabled = true;
            taken.OnNext(2);

            Assert.That(values, Is.EqualTo(new[] { 2 }));
            Assert.That(taken.IsDisposed, Is.True);
        }

        [Test]
        public void DirectInputIsCountedBySkip()
        {
            using var source = new EventStream<int>();
            using var skipped = source.Skip(2);
            var values = new List<int>();
            using var listener = skipped.Subscribe(values.Add);

            skipped.OnNext(1);
            source.OnNext(2);
            source.OnNext(3);

            Assert.That(values, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void DirectInputPassesThroughTakeWhileAndSkipWhileConditions()
        {
            using var source = new EventStream<int>();
            var positive = new Filter<int>("positive", value => value > 0);
            using var taken = source.TakeWhile(positive);
            using var skipped = source.SkipWhile(positive);
            var takenValues = new List<int>();
            var skippedValues = new List<int>();
            using var takenListener = taken.Subscribe(takenValues.Add);
            using var skippedListener = skipped.Subscribe(skippedValues.Add);

            taken.OnNext(1);
            skipped.OnNext(1);
            skipped.OnNext(-1);
            taken.OnNext(-1);

            Assert.That(takenValues, Is.EqualTo(new[] { 1 }));
            Assert.That(skippedValues, Is.EqualTo(new[] { -1 }));
            Assert.That(taken.IsDisposed, Is.True);
        }

        [Test]
        public void DirectInputReachesOnlyTheMergedStream()
        {
            using var left = new EventStream<int>();
            using var right = new EventStream<int>();
            using var merged = left.Merge(right);
            var leftValues = new List<int>();
            var mergedValues = new List<int>();
            using var original = left.Subscribe(leftValues.Add);
            using var combined = merged.Subscribe(mergedValues.Add);

            merged.OnNext(1);
            left.OnNext(2);
            right.OnNext(3);

            Assert.That(leftValues, Is.EqualTo(new[] { 2 }));
            Assert.That(mergedValues, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        // C7: A stream can be handed to any source as a plain observer.
        [TestCase(false)]
        [TestCase(true)]
        public void StreamReceivesValuesAndTerminationAsAnObserver(bool error)
        {
            using var source = new EventStream<int>();
            using var target = new EventStream<int>();
            var values = new List<int>();
            var completed = 0;
            var errors = new List<Exception>();
            var failure = new ApplicationException("source");
            using var listener = target.Subscribe(values.Add, () => completed++, errors.Add);
            IObserver<int> observer = target;
            using var connection = source.Subscribe(observer);

            source.OnNext(1);
            if (error) source.OnError(failure);
            else source.OnCompleted();

            Assert.That(values, Is.EqualTo(new[] { 1 }));
            Assert.That(completed, Is.EqualTo(error ? 0 : 1));
            Assert.That(errors, Is.EqualTo(error ? new[] { failure } : Array.Empty<Exception>()));
            Assert.That(target.IsDisposed, Is.True);
        }

        [Test]
        public void ObserverConnectionStaysUnderTheControlOfItsHandle()
        {
            using var source = new EventStream<int>();
            using var target = new EventStream<int>();
            var values = new List<int>();
            using var listener = target.Subscribe(values.Add);
            var connection = source.Subscribe(target);

            source.OnNext(1);
            connection.Dispose();
            source.OnNext(2);

            Assert.That(values, Is.EqualTo(new[] { 1 }));
            Assert.That(target.IsDisposed, Is.False);
            Assert.That(source.IsDisposed, Is.False);
        }

        [Test]
        public void DisposedObserverStreamIsReleasedLikeAFailedSubscriber()
        {
            using var source = new EventStream<int>();
            var target = new EventStream<int>();
            var reported = new List<Exception>();
            ExceptionHandler.SetHandler(reported.Add);
            try
            {
                var connection = (Handle)source.Subscribe(target);
                target.Dispose();

                source.OnNext(1);
                source.OnNext(2);

                Assert.That(connection.IsDisposed, Is.True);
                Assert.That(reported, Has.Count.EqualTo(1));
                Assert.That(reported[0], Is.TypeOf<ObjectDisposedException>());
            }
            finally
            {
                ExceptionHandler.SetHandler(null);
            }
        }

        [Test]
        public void DirectInputDuringTheSameRelationPublicationIsRejected()
        {
            using var source = new EventStream<int>();
            using var branch = source.Branch();
            var failures = new List<Exception>();
            using var reentrant = branch.Subscribe(_ =>
            {
                try
                {
                    branch.OnNext(2);
                }
                catch (InvalidOperationException exception)
                {
                    failures.Add(exception);
                }
            });

            source.OnNext(1);

            Assert.That(failures, Has.Count.EqualTo(1));
        }
    }
}
