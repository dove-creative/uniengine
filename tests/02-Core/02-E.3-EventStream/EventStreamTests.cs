using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamTests
    {
        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // R1: Root mutations commit after publication; additions receive the next value.
        [Test]
        public void EventStreamAppliesSubscriptionChangesAtTheExpectedTime()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var removed = (IDisposable)null;

            stream.Subscribe(value =>
            {
                calls.Add($"first:{value}");

                if (value != 1) return;

                removed.Dispose();
                stream.Subscribe(next => calls.Add($"late:{next}"));
            });

            removed = stream.Subscribe(value => calls.Add($"removed:{value}"));
            stream.OnNext(1);
            stream.OnNext(2);
            Assert.That(calls, Is.EquivalentTo(new[] { "first:1", "removed:1", "first:2", "late:2" }));
        }

        // R1: Fault isolation or propagation still permits a later publication.
        [TestCase(false)]
        [TestCase(true)]
        public void EventStreamRecoversAfterCallbackFailure(bool handled)
        {
            using var stream = new EventStream<int>();
            var calls = new List<int>();
            var failure = new ApplicationException("subscriber");

            var reported = new List<Exception>();
            if (handled) ExceptionHandler.SetHandler(reported.Add);

            stream.Subscribe(_ => throw failure);
            stream.Subscribe(calls.Add);

            if (handled)
            {
                stream.OnNext(1);
                Assert.That(reported, Is.EqualTo(new[] { failure }));
            }
            else
            {
                Assert.That(Assert.Throws<ApplicationException>(() => stream.OnNext(1)), Is.SameAs(failure));
            }

            stream.OnNext(2);
            // A failed publication does not prevent the next publication.
            Assert.That(calls, Is.EqualTo(handled ? new[] { 1, 2 } : new[] { 2 }));
        }

        // R1: Disposal inside a root callback finishes the unit and cleans up.
        [Test]
        public void EventStreamDisposalFinishesTheRootUnit()
        {
            using var stream = new EventStream<int>();
            var laterCalls = 0;
            stream.Subscribe(_ => stream.Dispose());
            stream.Subscribe(_ => laterCalls++);
            stream.OnNext(1);
            Assert.That(laterCalls, Is.EqualTo(1));
            Assert.That(stream.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => stream.OnNext(2));
        }

        // Skip stops counting once its prefix is consumed, so the counter never wraps around.
        [TestCase(2, new[] { 3, 4 })]
        [TestCase(0, new[] { 1, 2, 3, 4 })]
        [TestCase(int.MinValue, new[] { 1, 2, 3, 4 })]
        public void SkipForwardsEveryValueAfterTheSkippedPrefix(int count, int[] expected)
        {
            using var source = new EventStream<int>();
            var skipped = source.Skip(count);
            var values = new List<int>();
            skipped.Subscribe(values.Add);

            for (var value = 1; value <= 4; value++) source.OnNext(value);

            Assert.That(values, Is.EqualTo(expected));
        }

        // Merge rejects disposed inputs and leaves the valid input untouched.
        [TestCase(true)]
        [TestCase(false)]
        public void MergeRejectsDisposedInputsAndLeavesTheOtherInputConnected(bool disposedFirst)
        {
            using var source = new EventStream<int>();
            using var disposed = new EventStream<int>();
            var values = new List<int>();
            using var subscription = source.Subscribe(values.Add);
            disposed.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
            {
                using var merged = disposedFirst ? disposed.Merge(source) : source.Merge(disposed);
            });

            source.OnNext(7);
            Assert.That(values, Is.EqualTo(new[] { 7 }));
            Assert.That(source.NodeCount, Is.Zero, "Failed construction must not leave an upstream connection.");
        }

        // Merge completes after every input completes; null entries remain optional.
        [Test]
        public void MergeCompletesAfterEveryInputCompletes()
        {
            using var left = new EventStream<int>();
            using var right = new EventStream<int>();
            using var merged = left.Merge(null, right);
            var values = new List<int>();
            var completions = 0;
            using var subscription = merged.Subscribe(values.Add, () => completions++, null);

            left.OnNext(1);
            right.OnNext(2);
            left.OnCompleted();
            Assert.That(merged.IsDisposed, Is.False);

            right.OnNext(3);
            right.OnCompleted();
            Assert.That(values, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(merged.IsDisposed, Is.True);
        }

        // A value that reaches two inputs is delivered once for each input.
        [Test]
        public void MergeDeliversAValueForEachInputThatPassesIt()
        {
            using var source = new EventStream<int>();
            Func<int, bool> positive = value => value > 0;
            Func<int, bool> even = value => value % 2 == 0;
            using var merged = source.Where(positive).Branch().Merge(source.Where(even).Branch());
            var values = new List<int>();
            using var subscription = merged.Subscribe(values.Add);

            source.OnNext(1);
            source.OnNext(2);
            source.OnNext(-2);
            source.OnNext(-1);

            Assert.That(values, Is.EqualTo(new[] { 1, 2, 2, -2 }));
        }

        // Disposal of one input leaves the remaining inputs in charge of completion.
        [Test]
        public void MergeCompletesWithTheRemainingInputAfterAnotherInputIsDisposed()
        {
            using var left = new EventStream<int>();
            using var right = new EventStream<int>();
            using var merged = left.Merge(right);
            var values = new List<int>();
            var notifications = 0;
            using var subscription = merged.Subscribe(values.Add, () => notifications++, _ => notifications++);

            left.Dispose();
            Assert.That(merged.IsDisposed, Is.False);
            Assert.That(notifications, Is.Zero);

            right.OnNext(1);
            right.OnCompleted();

            Assert.That(values, Is.EqualTo(new[] { 1 }));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(merged.IsDisposed, Is.True);
        }

        [Test]
        public void MergeIsSilentlyReleasedWithItsLastDisposedInput()
        {
            using var left = new EventStream<int>();
            using var right = new EventStream<int>();
            using var merged = left.Merge(right);
            var notifications = 0;
            using var subscription = merged.Subscribe(_ => { }, () => notifications++, _ => notifications++);

            left.Dispose();
            right.Dispose();

            Assert.That(merged.IsDisposed, Is.True);
            Assert.That(notifications, Is.Zero);
        }

        // A pending merge connects only to the inputs that are still alive.
        [Test]
        public void PendingMergeSkipsAnInputThatEndedBeforeTheFirstSubscription()
        {
            using var left = new EventStream<int>();
            using var right = new EventStream<int>();
            using var merged = left.Merge(right);
            left.Dispose();
            var values = new List<int>();
            using var subscription = merged.Subscribe(values.Add);

            right.OnNext(1);

            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }
    }
}
