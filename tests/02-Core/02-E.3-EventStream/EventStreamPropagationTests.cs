using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamPropagationTests
    {
        // Content --------------------
        // Subscription changes
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        [Test]
        public void SelfRemovalFinishesTheMulticastSubscription()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var subscription = (IDisposable)null;

            Action<int> callback = value =>
            {
                calls.Add($"first:{value}");
                subscription.Dispose();
            };

            callback += value => calls.Add($"tail:{value}");
            subscription = stream.Subscribe(callback);
            using var other = stream.Subscribe(value => calls.Add($"other:{value}"));

            stream.OnNext(1);
            stream.OnNext(2);

            Assert.That(calls, Is.EquivalentTo(new[] { "first:1", "tail:1", "other:1", "other:2" }));
        }

        [Test]
        public void RemovingAnEarlierDuplicateDoesNotInterruptTheCurrentRegistration()
        {
            using var stream = new EventStream<int>();
            var firstCalls = 0;
            var tailCalls = 0;
            var first = (IDisposable)null;

            Action<int> callback = _ =>
            {
                firstCalls++;

                if (firstCalls == 2) first.Dispose();
            };

            callback += _ => tailCalls++;
            first = stream.Subscribe(callback);
            using var second = stream.Subscribe(callback);

            stream.OnNext(1);
            Assert.That(firstCalls, Is.EqualTo(2));
            Assert.That(tailCalls, Is.EqualTo(2));
            stream.OnNext(2);
            Assert.That(firstCalls, Is.EqualTo(3));
            Assert.That(tailCalls, Is.EqualTo(3));
            second.Dispose();
            stream.OnNext(3);
            Assert.That(firstCalls, Is.EqualTo(3));
        }

        [Test]
        public void OrderedPublicationDefersNewGroupsAndSkipsRemovedPendingSubscriptions()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var removed = (IDisposable)null;

            using var early = stream.ForOrder(Order.Early).Subscribe(value =>
            {
                calls.Add($"early:{value}");

                if (value != 1) return;

                removed.Dispose();
                stream.ForOrder(Order.Middle).Subscribe(next => calls.Add($"middle:{next}"));
                stream.ForOrder(Order.Late).Subscribe(next => calls.Add($"late:{next}"));
                var cancelled = stream.ForOrder(Order.Middle).Subscribe(next => calls.Add($"cancelled:{next}"));
                cancelled.Dispose();
            });

            removed = stream.ForOrder(Order.Middle).Subscribe(value => calls.Add($"removed:{value}"));

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "early:1" }));
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(new[] { "early:1", "early:2", "middle:2", "late:2" }));
        }

        [Test]
        public void FilteredPublicationKeepsFilteringAndDeferredSubscriptionChanges()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var removed = (IDisposable)null;
            Func<int, bool> odd = value => value % 2 == 1;
            Func<int, bool> large = value => value >= 2;

            using var first = stream.Where(odd).Subscribe(value =>
            {
                calls.Add($"first:{value}");
                removed.Dispose();
                stream.Where(large).Subscribe(next => calls.Add($"late:{next}"));
            });

            removed = stream.Where(large).Subscribe(value => calls.Add($"removed:{value}"));
            using var ordinary = stream.Subscribe(value => calls.Add($"ordinary:{value}"));
            using var empty = stream.Where(_ => false).Subscribe(
                value => calls.Add($"empty:{value}"));

            stream.OnNext(3);
            stream.OnNext(2);

            Assert.That(calls, Is.EquivalentTo(new[] { "ordinary:3", "first:3", "removed:3", "ordinary:2", "late:2" }));
        }

        [Test]
        public void NestedFilteredPublicationKeepsTheOuterValueForRemainingSubscribers()
        {
            var reported = new List<Exception>();
            ExceptionHandler.SetHandler(reported.Add);
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            Func<int, bool> one = value => value == 1;
            Func<int, bool> two = value => value == 2;
            using var nested = stream.Where(one).Subscribe(_ => stream.OnNext(2));
            using var matched = stream.Where(one).Subscribe(value => calls.Add($"matched:{value}"));
            using var other = stream.Where(two).Subscribe(value => calls.Add($"other:{value}"));

            stream.OnNext(1);

            Assert.That(calls, Is.EqualTo(new[] { "matched:1" }));
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported[0], Is.TypeOf<InvalidOperationException>());
        }

        // Failure and termination
        [Test]
        public void DisposalFinishesTheMulticastAndTheRootUnit()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();

            Action<int> callback = _ =>
            {
                calls.Add("first");
                stream.Dispose();
            };

            callback += _ => calls.Add("tail");
            stream.Subscribe(callback);
            stream.Subscribe(_ => calls.Add("other"));

            stream.OnNext(1);

            Assert.That(calls, Is.EquivalentTo(new[] { "first", "tail", "other" }));
            Assert.That(stream.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => stream.OnNext(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MulticastFailureRemovesTheRegistrationAndPreservesExceptionPolicy(bool handled)
        {
            using var stream = new EventStream<int>();
            var calls = new List<int>();
            var failure = new ApplicationException("subscriber");

            var reported = new List<Exception>();
            if (handled) ExceptionHandler.SetHandler(reported.Add);

            Action<int> callback = _ => throw failure;
            callback += _ => Assert.Fail("The remaining multicast callback must not run.");
            using var failed = stream.Subscribe(callback);
            using var other = stream.Subscribe(calls.Add);

            if (handled) stream.OnNext(1);
            else Assert.That(Assert.Throws<ApplicationException>(() => stream.OnNext(1)), Is.SameAs(failure));

            stream.OnNext(2);

            // An unhandled failure stops this value but leaves the next publication available.
            Assert.That(calls, Is.EqualTo(handled ? new[] { 1, 2 } : new[] { 2 }));
            Assert.That(reported, Is.EqualTo(handled ? new[] { failure } : Array.Empty<Exception>()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TerminalNotificationHonorsMulticastRemovalAndDisposesTheStream(bool error)
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var failure = new ApplicationException("terminal");
            var subscription = (IDisposable)null;

            Action completed = () =>
            {
                calls.Add("completed");
                subscription.Dispose();
            };

            completed += () => calls.Add("completed tail");

            Action<Exception> errored = exception =>
            {
                Assert.That(exception, Is.SameAs(failure));
                calls.Add("errored");
                subscription.Dispose();
            };

            errored += _ => calls.Add("errored tail");
            subscription = stream.Subscribe(null, completed, errored);
            using var other = stream.Subscribe(
                null,
                () => calls.Add("other completed"),
                _ => calls.Add("other errored"));

            if (error) stream.OnError(failure);
            else stream.OnCompleted();

            stream.OnCompleted();
            stream.OnError(failure);

            Assert.That(calls, Is.EquivalentTo(error
                ? new[] { "errored", "errored tail", "other errored" }
                : new[] { "completed", "completed tail", "other completed" }));
            Assert.That(stream.IsDisposed, Is.True);
        }

        // Lifetime
        [TestCase(false)]
        [TestCase(true)]
        public void ReleasedOriginalCallbacksAndTargetsAreNotRetained(bool disposeStream)
        {
            using var stream = new EventStream<int>();
            var references = RegisterAndRelease(stream, disposeStream);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.That(references.Target.IsAlive, Is.False);
            Assert.That(references.Updated.IsAlive, Is.False);
            Assert.That(references.Completed.IsAlive, Is.False);
            Assert.That(references.Errored.IsAlive, Is.False);
            GC.KeepAlive(references.Handle);
            GC.KeepAlive(stream);
        }

        // Helpers
        private enum Order { Early, Middle, Late }

        private class Receiver
        {
            public int Value { get; private set; }
            public void OnNext(int value) => Value += value;
            public void OnCompleted() => Value++;
            public void OnError(Exception exception) => Value++;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (
            WeakReference Target,
            WeakReference Updated,
            WeakReference Completed,
            WeakReference Errored,
            IDisposable Handle) RegisterAndRelease(EventStream<int> stream, bool disposeStream)
        {
            var receiver = new Receiver();
            Action<int> updated = receiver.OnNext;
            updated += receiver.OnNext;
            Action completed = receiver.OnCompleted;
            Action<Exception> errored = receiver.OnError;
            var handle = stream.Subscribe(updated, completed, errored);
            stream.OnNext(1);

            if (disposeStream) stream.Dispose();
            else handle.Dispose();

            return (
                new WeakReference(receiver),
                new WeakReference(updated),
                new WeakReference(completed),
                new WeakReference(errored),
                handle);
        }
    }
}
