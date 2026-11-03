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

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SelfRemovalSkipsTheRestOfTheMulticastSubscription(int kind)
        {
            using var stream = CreateStream(kind);
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

            Assert.That(calls, Is.EqualTo(new[] { "first:1", "other:1", "other:2" }));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void RemovingAnEarlierDuplicateDoesNotInterruptTheCurrentRegistration(int kind)
        {
            using var stream = CreateStream(kind);
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
            using var stream = new OrderedEventStream<int, Order>(Order.Early, new[] { Order.Early, Order.Middle });
            var calls = new List<string>();
            var removed = (IDisposable)null;
            using var early = stream.Subscribe(Order.Early, value =>
            {
                calls.Add($"early:{value}");
                if (value != 1) return;
                removed.Dispose();
                stream.Subscribe(Order.Middle, next => calls.Add($"middle:{next}"));
                stream.Subscribe(Order.Late, next => calls.Add($"late:{next}"));
                var cancelled = stream.Subscribe(Order.Middle, next => calls.Add($"cancelled:{next}"));
                cancelled.Dispose();
            });
            removed = stream.Subscribe(Order.Middle, value => calls.Add($"removed:{value}"));

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "early:1" }));
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(new[] { "early:1", "early:2", "middle:2", "late:2" }));
            Assert.That(stream.Orders, Is.EqualTo(new[] { Order.Early, Order.Middle, Order.Late }));
        }

        [Test]
        public void RoutedPublicationKeepsMaskFilteringAndDeferredSubscriptionChanges()
        {
            using var router = EventRouterExtensions.Route<int>(value => (uint)value);
            var calls = new List<string>();
            var removed = (IDisposable)null;
            using var first = router.Subscribe(1, value =>
            {
                calls.Add($"first:{value}");
                removed.Dispose();
                router.Subscribe(2, next => calls.Add($"late:{next}"));
            });
            removed = router.Subscribe(2, value => calls.Add($"removed:{value}"));
            using var ordinary = router.Subscribe(value => calls.Add($"ordinary:{value}"));
            using var empty = router.Subscribe(Array.Empty<int>(), value => calls.Add($"empty:{value}"));

            router.OnNext(3);
            router.OnNext(2);

            Assert.That(calls, Is.EqualTo(new[] { "first:3", "ordinary:3", "ordinary:2", "late:2" }));
        }

        [Test]
        public void NestedRoutedPublicationKeepsTheOuterMaskForRemainingSubscribers()
        {
            var reported = new List<Exception>();
            ExceptionHandler.SetHandler(reported.Add);
            using var router = EventRouterExtensions.Route<int>(value => (uint)value);
            var calls = new List<string>();
            using var nested = router.Subscribe(1, _ => router.OnNext(2));
            using var matched = router.Subscribe(1, value => calls.Add($"matched:{value}"));
            using var other = router.Subscribe(2, value => calls.Add($"other:{value}"));

            router.OnNext(1);

            Assert.That(calls, Is.EqualTo(new[] { "matched:1" }));
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported[0], Is.TypeOf<InvalidOperationException>());
        }

        // Failure and termination
        [TestCase(1)]
        [TestCase(2)]
        public void DisposalStopsTheMulticastTailAndLaterSubscribers(int kind)
        {
            using var stream = CreateStream(kind);
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

            Assert.That(calls, Is.EqualTo(new[] { "first" }));
            Assert.That(stream.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => stream.OnNext(2));
        }

        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(3, true)]
        public void MulticastFailureRemovesTheRegistrationAndPreservesExceptionPolicy(int kind, bool handled)
        {
            using var stream = CreateStream(kind);
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

            Assert.That(calls, Is.EqualTo(handled ? new[] { 1, 2 } : new[] { 2 }));
            Assert.That(reported, Is.EqualTo(handled ? new[] { failure } : Array.Empty<Exception>()));
        }

        [TestCase(0, false)]
        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        public void TerminalNotificationHonorsMulticastRemovalAndDisposesTheStream(int kind, bool error)
        {
            using var stream = CreateStream(kind);
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
            using var other = stream.Subscribe(null, () => calls.Add("other completed"), _ => calls.Add("other errored"));

            if (error) stream.OnError(failure);
            else stream.OnCompleted();
            stream.OnCompleted();
            stream.OnError(failure);

            Assert.That(calls, Is.EqualTo(error
                ? new[] { "errored", "other errored" } : new[] { "completed", "other completed" }));
            Assert.That(stream.IsDisposed, Is.True);
        }

        // Compatibility and lifetime
        [Test]
        public void LegacyDispatchStillFiltersAndRemovesRawFailingCallbacks()
        {
            using var stream = new LegacyStream();
            var calls = new List<int>();
            var reported = new List<Exception>();
            var failure = new ApplicationException("raw subscriber");
            ExceptionHandler.SetHandler(reported.Add);
            Action<int> callback = _ => throw failure;
            callback += _ => Assert.Fail("The raw multicast tail must not run after failure.");
            stream.AddRaw(callback);
            stream.Excluded = _ => Assert.Fail("The legacy filter must still apply.");
            stream.AddRaw(stream.Excluded);
            stream.AddRaw(calls.Add);

            stream.OnNext(1);
            stream.OnNext(2);

            Assert.That(calls, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(reported, Is.EqualTo(new[] { failure }));
        }

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

        private class LegacyStream : EventStream<int>
        {
            public Action<int> Excluded { get; set; }

            public void AddRaw(Action<int> callback) => Updated.Add(callback);

            public override void OnNext(int value)
            {
                ThrowIfDisposed();
                if (!Enabled) return;
                Dispatch(Updated, callback => callback(value), callback => callback != Excluded);
            }
        }

        private class Receiver
        {
            public int Value { get; private set; }
            public void OnNext(int value) => Value += value;
            public void OnCompleted() => Value++;
            public void OnError(Exception exception) => Value++;
        }

        private static IEventStream<int> CreateStream(int kind) =>
            kind switch
            {
                0 => new EventStream<int>(),
                1 => new OrderedEventStream<int, Order>(),
                2 => EventRouterExtensions.Route<int>(value => (uint)value),
                3 => new LegacyStream(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Target, WeakReference Updated, WeakReference Completed, WeakReference Errored, IDisposable Handle)
            RegisterAndRelease(EventStream<int> stream, bool disposeStream)
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
            return (new WeakReference(receiver), new WeakReference(updated), new WeakReference(completed), new WeakReference(errored), handle);
        }
    }
}
