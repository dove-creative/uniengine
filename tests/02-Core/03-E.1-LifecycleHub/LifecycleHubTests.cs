using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Blackthunder.UniEngine.Core;
using BlackThunder.UniEngine.Infrastructure;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class LifecycleHubTests
    {
        // Content --------------------
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // Connections
        [Test]
        public void ConnectAfterDisposeThrows()
        {
            var hub = CreateLifecycleHub(out var dispose);
            dispose();

            Assert.That(hub.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => hub.Connect(new object()));
        }

        [Test]
        public void DisconnectedListenerIsNotNotifiedOnDispose()
        {
            var hub = CreateLifecycleHub(out var dispose);
            var listener = new object();
            var notifications = 0;
            hub.Connect(listener).OnDisposed(() => notifications++);

            hub.Disconnect(listener);
            dispose();

            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void DisconnectIgnoresUnknownAndNullListenersAndDisposedHubs()
        {
            var hub = CreateLifecycleHub(out var dispose);
            var listener = new object();
            var notifications = 0;
            hub.Connect(listener).OnDisposed(() => notifications++);

            hub.Disconnect(new object());
            hub.Disconnect(null);
            dispose();

            Assert.That(notifications, Is.EqualTo(1));
            Assert.DoesNotThrow(() => hub.Disconnect(listener));
        }

        // Disposal
        [Test]
        public void DisposeNotifiesListenersOnceAndIgnoresRepeatedCalls()
        {
            var hub = CreateLifecycleHub(out var dispose);
            var notifications = 0;
            hub.Connect(new object()).OnDisposed(() => notifications++);
            Assert.That(hub.IsDisposed, Is.False);

            dispose();
            Assert.That(hub.IsDisposed, Is.True);
            Assert.That(notifications, Is.EqualTo(1));

            Assert.DoesNotThrow(() => dispose());
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void DisposalTraversesRegisteredCallbacksWithoutAllocating()
        {
            PrepareDisposal(new Counter())();
            var counter = new Counter();
            var dispose = PrepareDisposal(counter);

            var bytes = MeasureDisposal(dispose);
            Assert.That(counter.Calls, Is.EqualTo(4));
            Assert.That(bytes, Is.Zero);
        }

        // Direct subscriptions
        [Test]
        public void DirectDisposalRejectsDuplicateRegistrationAndKeepsTheOriginal()
        {
            var hub = new LifecycleHub();
            var calls = new List<string>();
            Action repeated = () => calls.Add("repeated");
            hub.OnDisposed += repeated;
            hub.OnDisposed += () => calls.Add("middle");

            Assert.Throws<InvalidOperationException>(() => hub.OnDisposed += repeated);
            hub.Dispose();

            Assert.That(calls, Is.EquivalentTo(new[] { "repeated", "middle" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectDisposalMulticastRemovalRequiresTheWholeRegistration(bool removeWhole)
        {
            var hub = new LifecycleHub();
            var calls = new List<string>();
            Action first = () => calls.Add("first");
            Action second = () => calls.Add("second");
            var registration = first + second;
            hub.OnDisposed += registration;
            hub.OnDisposed += () => calls.Add("other");

            hub.OnDisposed -= removeWhole ? registration : second;
            hub.Dispose();

            Assert.That(calls, Is.EquivalentTo(removeWhole ? new[] { "other" } : new[] { "first", "second", "other" }));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void CallbackFailureSkipsItsMulticastTail(bool direct, bool handled)
        {
            var hub = new LifecycleHub();
            var calls = new List<string>();
            var failure = new ApplicationException("disposed");
            var reports = new List<Exception>();
            if (handled) ExceptionHandler.SetHandler(reports.Add);

            Action bad = () =>
            {
                calls.Add("bad");
                throw failure;
            };

            var registration = bad + (() => calls.Add("tail"));
            if (direct) hub.OnDisposed += registration;
            else hub.Connect(new object()).OnDisposed(registration);

            if (handled)
            {
                Action other = () => calls.Add("other");
                if (direct) hub.Connect(new object()).OnDisposed(other);
                else hub.OnDisposed += other;
            }

            if (handled) hub.Dispose();
            else Assert.That(Assert.Throws<ApplicationException>(hub.Dispose), Is.SameAs(failure));

            Assert.That(calls, Is.EquivalentTo(handled ? new[] { "bad", "other" } : new[] { "bad" }));
            Assert.That(reports, Is.EqualTo(handled ? new[] { failure } : Array.Empty<Exception>()));
            Assert.That(hub.IsDisposed, Is.True);
            Assert.DoesNotThrow(hub.Dispose);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectAndClientRegistrationsShareTheSameKey(bool directFirst)
        {
            var hub = new LifecycleHub();
            var calls = new List<string>();
            Action direct = () => calls.Add("direct");
            Action connected = () => calls.Add("connected");

            if (directFirst)
            {
                hub.OnDisposed += direct;
                Assert.Throws<InvalidOperationException>(() => hub.Connect(direct).OnDisposed(connected));
            }
            else
            {
                hub.Connect(direct).OnDisposed(connected);
                Assert.Throws<InvalidOperationException>(() => hub.OnDisposed += direct);
            }

            hub.Dispose();

            Assert.That(calls, Is.EqualTo(new[] { directFirst ? "direct" : "connected" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RegistrationsCanBeRemovedThroughEitherEntryPoint(bool directRegistration)
        {
            var hub = new LifecycleHub();
            var calls = 0;
            Action callback = () => calls++;

            if (directRegistration)
            {
                hub.OnDisposed += callback;
                hub.Disconnect(callback);
            }
            else
            {
                hub.Connect(callback).OnDisposed(callback);
                hub.OnDisposed -= callback;
            }

            hub.Dispose();

            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void NullSubscriptionsAreIgnoredAndNullClientCallbacksAreAllowed()
        {
            var hub = new LifecycleHub();
            var calls = 0;
            hub.OnDisposed += null;
            hub.OnDisposed -= null;
            hub.Connect(new object()).OnDisposed(null);
            hub.OnDisposed += () => calls++;

            hub.Dispose();

            Assert.That(calls, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => hub.OnDisposed += null);
            Assert.DoesNotThrow(() => hub.OnDisposed -= null);
        }

        [Test]
        public void DisposalFreezesBothRegistrationPathsBeforeNotifying()
        {
            var hub = new LifecycleHub();
            var client = new object();
            var states = new List<bool>();
            Action callback = null;
            callback = () =>
            {
                states.Add(hub.IsDisposed);
                hub.OnDisposed -= callback;
                hub.Disconnect(client);
                hub.Dispose();
                Assert.Throws<ObjectDisposedException>(() => hub.OnDisposed += callback);
                Assert.Throws<ObjectDisposedException>(() => hub.Connect(new object()));
            };

            hub.OnDisposed += callback;
            hub.Connect(client).OnDisposed(callback);
            hub.Dispose();

            Assert.That(states, Is.EqualTo(new[] { true, true }));
        }

        // Helpers
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasureDisposal(Action dispose)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            dispose();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static ILifecycleHub CreateLifecycleHub(out Action dispose)
        {
            var lifecycle = new LifecycleHub();
            dispose = lifecycle.Dispose;
            return lifecycle;
        }

        private static Action PrepareDisposal(Counter counter)
        {
            var lifecycle = new LifecycleHub();
            Action notify = counter.Tick;
            lifecycle.Connect(counter).OnDisposed(notify + notify);
            lifecycle.OnDisposed += notify + notify;
            return lifecycle.Dispose;
        }

        private sealed class Counter
        {
            public int Calls;

            public void Tick() => Calls++;
        }
    }
}
