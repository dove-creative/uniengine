using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventHubTests
    {
        // Content --------------------
        // Connections
        [Test]
        public void ConnectRegistersANewListenerForUpdates()
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var values = new List<int>();

            hub.Connect(listener).OnUpdated(values.Add);
            hub.Update(1);

            Assert.That(hub.HasListener(listener), Is.True);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void ConnectRejectsADuplicatedListenerAndKeepsItsConnection()
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var values = new List<int>();
            hub.Connect(listener).OnUpdated(values.Add);

            Assert.Throws<InvalidOperationException>(() => hub.Connect(listener));
            hub.Update(1);

            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void ConnectRejectsANullListener()
        {
            using var hub = new EventHub<int>(new object());

            var exception = Assert.Throws<ArgumentNullException>(() => hub.Connect(null));

            Assert.That(exception.ParamName, Is.EqualTo("listener"));
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConnectAfterDisposeThrows(bool eventHub)
        {
            var hub = CreateLifecycleHub(eventHub, out var dispose);
            dispose();

            Assert.That(hub.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => hub.Connect(new object()));
        }

        [Test]
        public void DisconnectRemovesTheListenerAndItsUpdates()
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var values = new List<int>();
            hub.Connect(listener).OnUpdated(values.Add);

            hub.Disconnect(listener);
            hub.Update(1);

            Assert.That(hub.HasListener(listener), Is.False);
            Assert.That(hub.ConnectionCount, Is.Zero);
            Assert.That(values, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisconnectedListenerIsNotNotifiedOnDispose(bool eventHub)
        {
            var hub = CreateLifecycleHub(eventHub, out var dispose);
            var listener = new object();
            var notifications = 0;
            hub.Connect(listener).OnDisposed(() => notifications++);

            hub.Disconnect(listener);
            dispose();

            Assert.That(notifications, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisconnectIgnoresUnknownAndNullListenersAndDisposedHubs(bool eventHub)
        {
            var hub = CreateLifecycleHub(eventHub, out var dispose);
            var listener = new object();
            var notifications = 0;
            hub.Connect(listener).OnDisposed(() => notifications++);

            hub.Disconnect(new object());
            hub.Disconnect(null);
            dispose();

            Assert.That(notifications, Is.EqualTo(1));
            Assert.DoesNotThrow(() => hub.Disconnect(listener));
        }

        // Updates
        [TestCase(false)]
        [TestCase(true)]
        public void FirstAndRepeatedTypedUpdatesAllocateNoManagedMemory(bool subscribe)
        {
            using var warm = CreateUpdateHub(subscribe, new Counter());
            for (var index = 0; index < 3000; index++) warm.Update(1);

            var counter = new Counter();
            using var measured = CreateUpdateHub(subscribe, counter);
            var before = GC.GetAllocatedBytesForCurrentThread();
            measured.Update(1);
            var firstBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            const int repetitions = 10000;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < repetitions; index++) measured.Update(1);
            var repeatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(counter.Calls, Is.EqualTo(subscribe ? 6 * (repetitions + 1) : 0));
            Assert.That(firstBytes, Is.Zero);
            Assert.That(repeatedBytes, Is.Zero);
        }

        [Test]
        public void UpdateDefersBuilderAndDirectAdditionsAndSkipsDisconnectedCallbacks()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var early = hub.Connect(new object());
            var later = hub.Connect(new object());
            early.OnUpdated(value =>
            {
                calls.Add($"early:{value}");
                if (value != 1) return;
                later.OnUpdated(next => calls.Add($"added:{next}"));
                hub.Updated += next => calls.Add($"added direct:{next}");
                hub.Update(99);
            });
            later.OnUpdated(value => calls.Add($"later:{value}"));
            var removed = new object();
            hub.Connect(removed).OnUpdated(value =>
            {
                calls.Add($"filter:{value}");
                hub.Disconnect(removed);
                return true;
            }, value => calls.Add($"removed:{value}"));
            hub.Updated += value => calls.Add($"direct:{value}");

            hub.Update(1);
            hub.Update(2);

            Assert.That(calls, Is.EqualTo(new[]
            {
                "early:1", "later:1", "filter:1", "direct:1",
                "early:2", "later:2", "added:2", "direct:2", "added direct:2",
            }));
        }

        [TestCase(null)]
        [TestCase("args")]
        public void UpdateDeliversTheGivenArgsToListeners(string args)
        {
            using var hub = new EventHub<string>(new object());
            var received = new List<string>();
            hub.Connect(new object()).OnUpdated(received.Add);

            hub.Update(args);

            Assert.That(received, Is.EqualTo(new[] { args }));
        }

        [Test]
        public void UpdateAfterDisposeThrows()
        {
            var hub = new EventHub<int>(new object());
            var values = new List<int>();
            hub.Connect(new object()).OnUpdated(values.Add);
            hub.Dispose();

            Assert.Throws<ObjectDisposedException>(() => hub.Update(1));
            Assert.That(values, Is.Empty);
        }

        [Test]
        public void UpdateNotifiesEveryListenerInConnectionOrder()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            hub.Connect(new object()).OnUpdated(value => calls.Add($"first:{value}"));
            hub.Connect(new object()).OnUpdated(value => calls.Add($"second:{value}"));

            hub.Update(1);

            Assert.That(hub.ConnectionCount, Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(new[] { "first:1", "second:1" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReconnectedListenerUsesOnlyItsNewRegistration(bool disconnectAgain)
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var calls = new List<string>();
            hub.Connect(listener).OnUpdated(value => calls.Add($"stale:{value}"));
            hub.Disconnect(listener);
            hub.Connect(listener).OnUpdated(value => calls.Add($"fresh:{value}"));

            if (disconnectAgain) hub.Disconnect(listener);
            hub.Update(1);

            Assert.That(hub.HasListener(listener), Is.EqualTo(!disconnectAgain));
            Assert.That(calls, Is.EqualTo(disconnectAgain ? Array.Empty<string>() : new[] { "fresh:1" }));
        }

        [Test]
        public void ListenersDisconnectedDuringUpdateMissTheRestOfIt()
        {
            using var hub = new EventHub<int>(new object());
            var first = new object();
            var second = new object();
            var calls = new List<string>();
            hub.Connect(first).OnUpdated(_ =>
            {
                calls.Add("first");
                hub.Disconnect(first);
                hub.Disconnect(second);
            }).OnUpdated(_ => calls.Add("first tail"));
            hub.Connect(second).OnUpdated(_ => calls.Add("second"));

            hub.Update(1);
            hub.Update(2);

            Assert.That(hub.HasListener(first), Is.False);
            Assert.That(hub.HasListener(second), Is.False);
            Assert.That(calls, Is.EqualTo(new[] { "first" }));
        }

        [Test]
        public void DirectUpdatedSubscriptionsAreAddedAndRemovedOneAtATime()
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            Action<int> record = values.Add;

            hub.Updated += record;
            hub.Updated += record;
            hub.Update(1);
            hub.Updated -= record;
            hub.Update(2);
            hub.Updated -= record;
            hub.Update(3);

            Assert.That(hub.ConnectionCount, Is.Zero);
            Assert.That(values, Is.EqualTo(new[] { 1, 1, 2 }));
        }

        // Sleep
        [TestCase(SleepEntry.Sleep)]
        [TestCase(SleepEntry.SleepWithToken)]
        [TestCase(SleepEntry.TrySleep)]
        [TestCase(SleepEntry.TrySleepWithResult)]
        public void SleepSkipsUpdatesUntilItsTokenAwakensTheHub(SleepEntry entry)
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            var awakenings = 0;
            hub.Connect(new object()).OnUpdated(values.Add);
            Assert.That(hub.IsAwake, Is.True);

            var token = StartSleep(hub, entry, () => awakenings++);
            hub.Update(1);
            Assert.That(hub.IsAwake, Is.False);
            Assert.That(values, Is.Empty);
            Assert.That(awakenings, Is.Zero);

            token.Dispose();
            Assert.That(hub.IsAwake, Is.True);
            Assert.That(awakenings, Is.EqualTo(1));
            Assert.That(values, Is.Empty);

            hub.Update(2);
            Assert.That(values, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void OnAwakeRunsAfterTheHubAwakensAndCanPublishAnUpdate()
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            hub.Connect(new object()).OnUpdated(values.Add);
            var token = hub.Sleep(() => hub.Update(1));

            hub.Update(0);
            token.Dispose();

            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void SleepingHubRejectsNestedSleepAndKeepsItsOriginalToken()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var token = hub.Sleep(() => calls.Add("outer"));

            Assert.Throws<InvalidOperationException>(() => hub.Sleep());
            Assert.Throws<InvalidOperationException>(() => hub.Sleep(new Token()));
            Assert.That(hub.TrySleep(out var rejected), Is.False);
            Assert.That(rejected, Is.Null);
            hub.TrySleep(() => calls.Add("nested")).Dispose();
            Assert.That(hub.IsAwake, Is.False);
            Assert.That(calls, Is.EqualTo(new[] { "nested" }));

            token.Dispose();
            Assert.That(hub.IsAwake, Is.True);
            Assert.That(calls, Is.EqualTo(new[] { "nested", "outer" }));
        }

        [TestCase(SleepEntry.Sleep)]
        [TestCase(SleepEntry.SleepWithToken)]
        [TestCase(SleepEntry.TrySleep)]
        [TestCase(SleepEntry.TrySleepWithResult)]
        public void DisposingTheHubIgnoresItsLaterAwakening(SleepEntry entry)
        {
            using var hub = new EventHub<int>(new object());
            var awakenings = 0;
            var token = StartSleep(hub, entry, () => awakenings++);

            hub.Dispose();
            token.Dispose();

            Assert.That(awakenings, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SleepRejectsAnInvalidTokenAndStaysAwake(bool disposedToken)
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            var awakenings = 0;
            hub.Connect(new object()).OnUpdated(values.Add);
            var token = disposedToken ? new Token() : null;
            token?.Dispose();

            var exception = Assert.Throws<ArgumentException>(() => hub.Sleep(token, () => awakenings++));
            hub.Update(1);

            Assert.That(exception.ParamName, Is.EqualTo("token"));
            Assert.That(hub.IsAwake, Is.True);
            Assert.That(awakenings, Is.Zero);
            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        // Disposal
        [TestCase(false)]
        [TestCase(true)]
        public void DisposeNotifiesListenersOnceAndIgnoresRepeatedCalls(bool eventHub)
        {
            var hub = CreateLifecycleHub(eventHub, out var dispose);
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
        public void DisposePassesTheOwnerToListenersAndReleasesEveryConnection()
        {
            var owner = new object();
            using var hub = new EventHub<int>(owner);
            var listener = new object();
            var owners = new List<object>();
            hub.Connect(listener).OnDisposing(owners.Add).OnDisposed(owners.Add);

            hub.Dispose();

            Assert.That(owners, Is.EqualTo(new[] { owner, owner }));
            Assert.That(hub.HasListener(listener), Is.False);
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [TestCase(false, 4)]
        [TestCase(true, 6)]
        public void DisposalTraversesRegisteredCallbacksWithoutAllocating(bool eventHub, int expectedCalls)
        {
            PrepareDisposal(eventHub, new Counter())();
            var counter = new Counter();
            var dispose = PrepareDisposal(eventHub, counter);

            var bytes = MeasureDisposal(dispose);
            Assert.That(counter.Calls, Is.EqualTo(expectedCalls));
            Assert.That(bytes, Is.Zero);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasureDisposal(Action dispose)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            dispose();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Test]
        public void DisposalSkipsRemovedDirectSubscriptionsAndDisconnectedListeners()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var listener = new object();
            var connected = hub.Connect(listener);
            connected.OnDisposing(() =>
            {
                calls.Add("listener disposing");
                hub.Disconnect(listener);
            }).OnDisposing(() => calls.Add("listener tail"))
                .OnDisposed(() => calls.Add("listener disposed"));
            Action removed = () => calls.Add("removed direct");
            hub.Disposing += () =>
            {
                calls.Add("direct disposing");
                hub.Disposing -= removed;
            };
            hub.Disposing += removed;
            hub.Disposed += () => calls.Add("direct disposed");

            hub.Dispose();

            Assert.That(calls, Is.EqualTo(new[] { "direct disposing", "listener disposing", "direct disposed" }));
        }

        [Test]
        public void DisposalFromUpdateWaitsForTheCurrentCallbackAndSkipsItsMulticastTail()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            Action<int> dispose = _ =>
            {
                calls.Add("before");
                hub.Dispose();
                calls.Add("after");
            };
            hub.Connect(new object()).OnUpdated(dispose + (_ => calls.Add("tail")));
            hub.Updated += _ => calls.Add("direct");
            hub.Disposing += () => calls.Add("disposing");
            hub.Disposed += () => calls.Add("disposed");

            hub.Update(0);

            Assert.That(hub.IsDisposed, Is.True);
            Assert.That(calls, Is.EqualTo(new[] { "before", "after", "disposing", "disposed" }));
        }

        [Test]
        public void DisposingASleepingHubReleasesItsTokenWithoutAwakening()
        {
            var counter = new Counter();
            using var token = new Token(counter.Tick);
            using var hub = new EventHub<int>(counter);
            hub.Sleep(token, counter.Tick);

            hub.Dispose();
            token.Dispose();

            Assert.That(counter.Calls, Is.EqualTo(1));
        }

        // Helpers
        private static ILifecycleHub CreateLifecycleHub(bool eventHub, out Action dispose)
        {
            if (eventHub)
            {
                var hub = new EventHub<int>(new object());
                dispose = hub.Dispose;
                return hub;
            }

            var lifecycle = new LifecycleHub();
            dispose = lifecycle.Dispose;
            return lifecycle;
        }

        private static IDisposable StartSleep(EventHub<int> hub, SleepEntry entry, Action onAwake)
        {
            switch (entry)
            {
                case SleepEntry.Sleep:
                    return hub.Sleep(onAwake);
                case SleepEntry.SleepWithToken:
                    var token = new Token();
                    Assert.That(hub.Sleep(token, onAwake), Is.SameAs(token));
                    return token;
                case SleepEntry.TrySleep:
                    return hub.TrySleep(onAwake);
                case SleepEntry.TrySleepWithResult:
                    Assert.That(hub.TrySleep(out var result, onAwake), Is.True);
                    return result;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry), entry, null);
            }
        }

        private static EventHub<int> CreateUpdateHub(bool subscribe, Counter counter)
        {
            var hub = new EventHub<int>(counter);
            if (!subscribe) return hub;

            Action<int> typed = counter.Record;
            Action plain = counter.Tick;
            hub.Connect(counter)
                .OnUpdated(typed + typed)
                .OnUpdated(1, typed)
                .OnUpdated(static value => value > 0, plain + plain);
            hub.Updated += typed;
            return hub;
        }

        private static Action PrepareDisposal(bool eventHub, Counter counter)
        {
            if (eventHub)
            {
                var hub = new EventHub<int>(counter, counter.Tick, counter.Tick);
                hub.Connect(counter).OnUpdated(counter.Record)
                    .OnDisposing(counter.Tick).OnDisposing(counter.RecordOwner)
                    .OnDisposed(counter.Tick).OnDisposed(counter.RecordOwner);
                hub.Updated += counter.Record;
                return hub.Dispose;
            }

            var lifecycle = new LifecycleHub();
            Action notify = counter.Tick;
            lifecycle.Connect(counter).OnDisposed(notify + notify);
            lifecycle.OnDisposed += notify + notify;
            return lifecycle.Dispose;
        }

        public enum SleepEntry
        {
            Sleep,
            SleepWithToken,
            TrySleep,
            TrySleepWithResult,
        }

        private sealed class Counter
        {
            public int Calls;

            public void Record(int value) => Calls += value;
            public void Tick() => Calls++;
            public void RecordOwner(object owner)
            {
                if (ReferenceEquals(owner, this)) Calls++;
            }
        }
    }
}
