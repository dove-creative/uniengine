using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Blackthunder.UniEngine.Core;
using BlackThunder.UniEngine.Infrastructure;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventHubFlagsTests
    {
        // Content --------------------
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // Matching and validation
        [TestCase(1, 1, true)]
        [TestCase(2, 1, false)]
        [TestCase(3, 5, true)]
        [TestCase(4, 3, false)]
        [TestCase(7, 3, true)]
        [TestCase(0, 7, false)]
        public void TypedHubInterfaceMatchesAnyMaskBitAndPreservesTheValue(int value, int mask, bool matches)
        {
            using var hub = new EventHub<ulong>(new object());
            IEventHub<ulong> source = hub;
            var values = new List<ulong>();
            source.Connect(new object()).OnFlags((Changes)mask, values.Add);

            hub.Update((ulong)value);

            Assert.That(values, Is.EqualTo(matches ? new[] { (ulong)value } : Array.Empty<ulong>()));
        }

        [Test]
        public void SignedMaskKeepsItsDeclaredWidthAndUnsignedMaskAcceptsTheHighestBit()
        {
            using var hub = new EventHub<ulong>(new object());
            var signed = new List<ulong>();
            var unsigned = new List<ulong>();
            var builder = hub.Connect(new object());
            builder.OnFlags(Changes.Sign, signed.Add);
            builder.OnFlags(WideChanges.Top, unsigned.Add);

            hub.Update(0x80000000UL);
            hub.Update(1UL << 63);
            hub.Update(0x8000000080000000UL);

            Assert.That(signed, Is.EqualTo(new[] { 0x80000000UL, 0x8000000080000000UL }));
            Assert.That(unsigned, Is.EqualTo(new[] { 1UL << 63, 0x8000000080000000UL }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidMasksAreRejectedDuringRegistrationAndLeaveExistingUpdatesIntact(bool parameterless)
        {
            using var hub = new EventHub<ulong>(new object());
            var listener = new object();
            var values = new List<ulong>();
            var builder = hub.Connect(listener);
            builder.OnUpdated(values.Add);

            var empty = Assert.Throws<ArgumentException>(() =>
            {
                if (parameterless) builder.OnFlags(Changes.None, (Action)null);
                else builder.OnFlags(Changes.None, (Action<ulong>)null);
            });

            var plain = Assert.Throws<ArgumentException>(() =>
            {
                if (parameterless) builder.OnFlags(PlainChange.Shape, (Action)null);
                else builder.OnFlags(PlainChange.Shape, (Action<ulong>)null);
            });
            hub.Update(1);
            hub.Update(2);

            Assert.That(empty.ParamName, Is.EqualTo("mask"));
            Assert.That(plain.ParamName, Is.EqualTo("mask"));
            Assert.That(hub.HasListener(listener), Is.True);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            Assert.That(values, Is.EqualTo(new ulong[] { 1, 2 }));
        }

        [Test]
        public void NullBuilderIsRejectedBeforeMaskValidation()
        {
            IEventHubBuilder<ulong> builder = null;

            var exception = Assert.Throws<ArgumentNullException>(() => builder.OnFlags(Changes.None, (Action)null));
            Assert.Throws<ArgumentNullException>(() => builder.OnFlags(PlainChange.Shape, (Action<ulong>)null));

            Assert.That(exception.ParamName, Is.EqualTo("builder"));
        }

        [Test]
        public void FlagsReturnsTheSameBuilderAndPreservesRootRegistrations()
        {
            using var hub = new EventHub<ulong>(new object());
            var listener = new object();
            var root = hub.Connect(listener);
            var roots = new List<ulong>();
            var typed = new List<ulong>();
            var plainCalls = 0;
            root.OnUpdated(roots.Add);

            Assert.That(root.OnFlags(Changes.Shape, typed.Add), Is.SameAs(root));
            Assert.That(root.OnFlags(Changes.Shape, () => plainCalls++), Is.SameAs(root));
            Assert.That(root.OnFlags(Changes.Shape, (Action<ulong>)null), Is.SameAs(root));
            Assert.That(root.OnFlags(Changes.Shape, (Action)null), Is.SameAs(root));
            Assert.Throws<InvalidOperationException>(() => root.OnFlags(Changes.Shape, typed.Add));

            hub.Update(2);
            hub.Update(3);

            Assert.That(roots, Is.EqualTo(new ulong[] { 2, 3 }));
            Assert.That(typed, Is.EqualTo(new ulong[] { 3 }));
            Assert.That(plainCalls, Is.EqualTo(1));
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            hub.Disconnect(listener);
            hub.Update(1);
            Assert.That(typed, Is.EqualTo(new ulong[] { 3 }));
            Assert.That(plainCalls, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FlagsAndWhenTargetDifferOnCompositeValues(bool parameterless)
        {
            using var hub = new EventHub<ulong>(new object());
            var builder = hub.Connect(new object());
            var flagsCalls = 0;
            var targetCalls = 0;
            var flagsValues = new List<ulong>();

            if (parameterless)
            {
                builder.OnFlags(Changes.Shape, () => flagsCalls++);
                builder.OnUpdated(Changes.Shape.ToBits(), () => targetCalls++);
            }
            else
            {
                builder.OnFlags(Changes.Shape, value => { flagsCalls++; flagsValues.Add(value); });
                builder.OnUpdated(Changes.Shape.ToBits(), _ => targetCalls++);
            }

            hub.Update((Changes.Shape | Changes.Location).ToBits());
            Assert.That(flagsCalls, Is.EqualTo(1));
            Assert.That(targetCalls, Is.Zero);
            if (!parameterless) Assert.That(flagsValues, Is.EqualTo(new ulong[] { 3 }));

            hub.Update(Changes.Shape.ToBits());
            Assert.That(flagsCalls, Is.EqualTo(2));
            Assert.That(targetCalls, Is.EqualTo(1));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void FlagsShortcutAndEquivalentConditionsRejectDuplicates(bool parameterless, bool conditionFirst)
        {
            using var hub = new EventHub<ulong>(new object());
            var builder = hub.Connect(new object());
            var calls = 0;
            Action callback = () => calls++;
            Action<ulong> typed = _ => calls++;
            var condition = EventCondition.Flags(RouteChanges.Shape);

            if (parameterless)
            {
                if (conditionFirst) builder.OnUpdated(condition, callback);
                else builder.OnFlags(Changes.Shape, callback);

                Assert.Throws<InvalidOperationException>(() => builder.OnFlags(RouteChanges.Shape, callback));
                Assert.Throws<InvalidOperationException>(() =>
                    builder.OnUpdated(EventCondition.Flags(Changes.Shape), callback));
                builder.OnFlags(Changes.Location, callback);
                builder.OnUpdated(callback);
            }
            else
            {
                if (conditionFirst) builder.OnUpdated(condition, typed);
                else builder.OnFlags(Changes.Shape, typed);

                Assert.Throws<InvalidOperationException>(() => builder.OnFlags(RouteChanges.Shape, typed));
                Assert.Throws<InvalidOperationException>(() =>
                    builder.OnUpdated(EventCondition.Flags(Changes.Shape), typed));
                builder.OnFlags(Changes.Location, typed);
                builder.OnUpdated(typed);
            }

            hub.Update(3);
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void FlagsConditionsValidateMasksBeforeRegistrationAndPreserveTheirPrefix()
        {
            using var hub = new EventHub<ulong>(new object());
            var shape = EventCondition.Flags(Changes.Shape);
            var both = shape.Flags(Changes.Location);
            var shapeValues = new List<ulong>();
            var bothValues = new List<ulong>();
            var builder = hub.Connect(new object());
            builder.OnUpdated(shape, shapeValues.Add).OnUpdated(both, bothValues.Add);

            Assert.Throws<ArgumentException>(() => EventCondition.Flags(Changes.None));
            Assert.Throws<ArgumentException>(() => EventCondition.Flags(PlainChange.Shape));
            Assert.Throws<ArgumentException>(() => shape.Flags(Changes.None));
            Assert.Throws<ArgumentException>(() => shape.Flags(PlainChange.Shape));
            var exception = Assert.Throws<ArgumentNullException>(() =>
                ((EventCondition<ulong>)null).Flags(Changes.None));
            Assert.That(exception.ParamName, Is.EqualTo("condition"));

            hub.Update(1);
            hub.Update(2);
            hub.Update(3);
            Assert.That(shapeValues, Is.EqualTo(new ulong[] { 1, 3 }));
            Assert.That(bothValues, Is.EqualTo(new ulong[] { 3 }));
        }

        // Sharing and composition
        [Test]
        public void EqualMaskBitsShareAcrossEnumTypesAndReleaseOnlyTheDisconnectedListener()
        {
            using var hub = new EventHub<ulong>(new object());
            var first = new object();
            var second = new object();
            var firstValues = new List<ulong>();
            var secondValues = new List<ulong>();
            var evaluations = 0;
            Func<ulong, bool> predicate = _ => { evaluations++; return true; };
            hub.Connect(first).OnUpdated(EventCondition.Flags(Changes.Shape).Where(predicate), firstValues.Add);
            hub.Connect(second).OnUpdated(EventCondition.Flags(RouteChanges.Shape).Where(predicate), secondValues.Add);

            hub.Update(3);
            Assert.That(evaluations, Is.EqualTo(1));
            hub.Disconnect(first);
            hub.Update(1);
            Assert.That(evaluations, Is.EqualTo(2));
            hub.Disconnect(second);
            hub.Update(1);

            Assert.That(evaluations, Is.EqualTo(2));
            Assert.That(firstValues, Is.EqualTo(new ulong[] { 3 }));
            Assert.That(secondValues, Is.EqualTo(new ulong[] { 3, 1 }));
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [Test]
        public void FlagsEntriesStayGroupedInCreationOrderAfterRootSubscriptions()
        {
            using var hub = new EventHub<ulong>(new object());
            var calls = new List<string>();
            var first = hub.Connect(new object());
            first.OnFlags(Changes.Location, () => calls.Add("location"));
            first.OnUpdated(_ => true, () => calls.Add("where"));
            var second = hub.Connect(new object());
            second.OnFlags(Changes.Shape, () => calls.Add("shape"));
            second.OnFlags(RouteChanges.Location, () => calls.Add("location-again"));
            first.OnUpdated(() => calls.Add("root"));
            hub.Updated += _ => calls.Add("direct");

            hub.Update(3);

            Assert.That(calls, Is.EqualTo(new[] { "root", "direct", "location", "location-again", "shape", "where" }));
        }

        [Test]
        public void FlagsConditionsComposeWithWhereDebugAndAnotherFlagsStep()
        {
            using var hub = new EventHub<ulong>(new object());
            var values = new List<ulong>();
            var condition = EventCondition.Where<ulong>(value => value < 8)
                .Flags(Changes.Shape | Changes.Location).Debug("flags", LogLevel.Warning)
                .Flags(RouteChanges.Location).Where(value => value > 2);
            hub.Connect(new object()).OnUpdated(condition, values.Add);
            using var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                hub.Update(0);
                hub.Update(1);
                hub.Update(2);
                hub.Update(3);
                hub.Update(9);
            }
            finally
            {
                Console.SetOut(original);
            }

            Assert.That(values, Is.EqualTo(new ulong[] { 3 }));
            Assert.That(output.ToString(), Does.Contain("1: flags").And.Contain("2: flags").And.Contain("3: flags"));
            Assert.That(output.ToString(), Does.Not.Contain("0: flags").And.Not.Contain("9: flags"));
        }

        // Mutation and lifetime
        [Test]
        public void UpdatesDeferNewFlagsSubscriptionsAndImmediatelyDisconnectExistingOnes()
        {
            using var hub = new EventHub<ulong>(new object());
            var calls = new List<string>();
            var removed = new object();
            var changed = false;
            hub.Connect(new object()).OnUpdated(() =>
            {
                calls.Add("root");
                if (changed) return;
                changed = true;
                hub.Connect(new object()).OnFlags(Changes.Shape, () => calls.Add("late-shape"));
                hub.Connect(new object()).OnFlags(Changes.Location, () => calls.Add("late-location"));
                hub.Disconnect(removed);
            });

            hub.Connect(new object()).OnFlags(Changes.Shape, () => calls.Add("shape"));
            hub.Connect(removed).OnFlags(Changes.Location, () => calls.Add("removed"));

            hub.Update(3);
            Assert.That(calls, Is.EqualTo(new[] { "root", "shape" }));
            calls.Clear();
            hub.Update(3);
            Assert.That(calls, Is.EqualTo(new[] { "root", "shape", "late-shape", "late-location" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisconnectInsideAFlagsCallbackSkipsItsMulticastTailAndOtherPaths(bool parameterless)
        {
            using var hub = new EventHub<ulong>(new object());
            var listener = new object();
            var builder = hub.Connect(listener);
            var tails = 0;
            var survivors = 0;
            Action disconnect = () => hub.Disconnect(listener);
            Action tail = () => tails++;

            if (parameterless) builder.OnFlags(Changes.Shape, disconnect + tail);
            else builder.OnFlags(Changes.Shape, new Action<ulong>(_ => disconnect()) + (_ => tail()));

            builder.OnFlags(Changes.Location, tail);
            hub.Connect(new object()).OnFlags(Changes.Location, () => survivors++);
            hub.Update(3);

            Assert.That(tails, Is.Zero);
            Assert.That(survivors, Is.EqualTo(1));
            Assert.That(hub.HasListener(listener), Is.False);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StaleFlagsBuildersCheckLifetimeBeforeMasksAndReconnectionDoesNotReviveThem(bool reconnect)
        {
            using var hub = new EventHub<ulong>(new object());
            var listener = new object();
            var root = hub.Connect(listener);
            root.OnFlags(Changes.Shape, () => { });
            var freshCalls = 0;
            hub.Disconnect(listener);
            if (reconnect) hub.Connect(listener).OnFlags(Changes.Shape, () => freshCalls++);

            Assert.Throws<InvalidOperationException>(() => root.OnFlags(Changes.None, (Action)null));
            Assert.Throws<InvalidOperationException>(() => root.OnFlags(PlainChange.Shape, (Action<ulong>)null));
            Assert.Throws<InvalidOperationException>(() => root.OnUpdated(() => { }));
            hub.Update(1);
            Assert.That(freshCalls, Is.EqualTo(reconnect ? 1 : 0));

            var active = hub.Connect(new object()).OnFlags(Changes.Shape, () => { });
            hub.Dispose();
            Assert.Throws<ObjectDisposedException>(() => root.OnFlags(Changes.None, (Action)null));
            Assert.Throws<ObjectDisposedException>(() => root.OnFlags(PlainChange.Shape, (Action<ulong>)null));
            Assert.Throws<ObjectDisposedException>(() => active.OnFlags(Changes.None, (Action)null));
        }

        [Test]
        public void FlagsUsesHubSleepAndLifecycleNotificationsIgnoreTheMask()
        {
            var owner = new object();
            using var hub = new EventHub<ulong>(owner);
            var values = new List<ulong>();
            var calls = new List<string>();
            hub.Connect(new object()).OnFlags(Changes.Shape, values.Add)
                .OnDisposing(value =>
                {
                    Assert.That(value, Is.SameAs(owner));
                    Assert.That(hub.IsDisposed, Is.False);
                    calls.Add("disposing");
                })
                .OnDisposed(() =>
                {
                    Assert.That(hub.IsDisposed, Is.True);
                    calls.Add("disposed");
                });

            using (hub.Sleep()) hub.Update(1);
            hub.Update(2);
            hub.Update(0);
            Assert.That(values, Is.Empty);
            hub.Dispose();

            Assert.That(calls, Is.EqualTo(new[] { "disposing", "disposed" }));
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [Test]
        public void FailedFlagsSubscriptionRemovesOnlyItsRegistrationAndAnEmptyListenerCanReconnect()
        {
            using var hub = new EventHub<ulong>(new object());
            var listener = new object();
            var survivor = new object();
            var failure = new ApplicationException("flags callback");
            var reports = new List<Exception>();
            var values = new List<ulong>();
            var disposed = 0;
            ExceptionHandler.SetHandler(reports.Add);
            var failed = hub.Connect(listener);
            failed.OnFlags(Changes.Shape, new Action<ulong>(_ => throw failure));
            var remaining = hub.Connect(survivor);
            remaining.OnFlags(Changes.Shape, new Action<ulong>(_ => throw failure));
            remaining.OnFlags(Changes.Shape, values.Add).OnDisposed(() => disposed++);

            hub.Update(1);
            Assert.That(hub.HasListener(listener), Is.False);
            Assert.That(hub.HasListener(survivor), Is.True);
            Assert.Throws<InvalidOperationException>(() => failed.OnFlags(Changes.Shape, values.Add));

            hub.Connect(listener).OnFlags(Changes.Location, values.Add);
            hub.Update(3);
            Assert.That(reports, Is.EqualTo(new[] { failure, failure }));
            Assert.That(values, Is.EqualTo(new ulong[] { 1, 3, 3 }));
            hub.Dispose();
            Assert.That(disposed, Is.EqualTo(1));
        }

        // Allocation
        [Test]
        public void FirstAndRepeatedFlagsUpdatesAllocateNoManagedMemory()
        {
            using var warm = CreateAllocationHub(new Counter());
            Publish(warm, 3000);

            var counter = new Counter();
            using var measured = CreateAllocationHub(counter);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Publish(measured, 1);
            var firstBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            const int repetitions = 10000;
            before = GC.GetAllocatedBytesForCurrentThread();
            Publish(measured, repetitions);
            var repeatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine($"EventHub flags: first={firstBytes}, repeated={repeatedBytes} bytes.");
            Assert.That(counter.Calls, Is.EqualTo(3 * (repetitions + 1)));
            Assert.That(firstBytes, Is.Zero);
            Assert.That(repeatedBytes, Is.Zero);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Publish(EventHub<ulong> hub, int repetitions)
        {
            for (var index = 0; index < repetitions; index++) hub.Update(3);
        }

        private static EventHub<ulong> CreateAllocationHub(Counter counter)
        {
            var hub = new EventHub<ulong>(counter);
            var builder = hub.Connect(counter);
            builder.OnFlags(Changes.Shape, counter.Record);
            builder.OnUpdated(EventCondition.Flags(RouteChanges.Shape).Where(static value => value > 0), counter.Tick);
            builder.OnFlags(Changes.Location, counter.Tick);
            return hub;
        }

        [Flags]
        private enum Changes
        {
            None = 0,
            Shape = 1,
            Location = 2,
            Sign = int.MinValue,
        }

        [Flags]
        private enum RouteChanges : byte { Shape = 1, Location = 2 }

        [Flags]
        private enum WideChanges : ulong { Top = 1UL << 63 }

        private enum PlainChange { Shape = 1 }

        private class Counter
        {
            public int Calls;

            public void Record(ulong value) => Calls++;
            public void Tick() => Calls++;
        }
    }
}
