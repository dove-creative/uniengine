using System;
using System.Collections.Generic;
using System.IO;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamFlagsTests
    {
        // Content --------------------
        // Matching and validation
        [TestCase(1, 1, true)]
        [TestCase(2, 1, false)]
        [TestCase(3, 5, true)]
        [TestCase(4, 3, false)]
        [TestCase(7, 3, true)]
        [TestCase(0, 7, false)]
        public void MatchesAnyMaskBitAndPreservesThePublishedValue(int value, int mask, bool matches)
        {
            using var stream = new EventStream<ulong>();
            var values = new List<ulong>();
            stream.ForFlags((GeoEvents)mask).Subscribe(values.Add);

            stream.OnNext((ulong)value);

            Assert.That(values, Is.EqualTo(matches ? new[] { (ulong)value } : Array.Empty<ulong>()));
        }

        [Test]
        public void SignedMaskUsesItsDeclaredWidthAndUnsignedMaskAcceptsTheHighestBit()
        {
            using var stream = new EventStream<ulong>();
            var signed = new List<ulong>();
            var unsigned = new List<ulong>();
            stream.ForFlags(GeoEvents.Sign).Subscribe(signed.Add);
            stream.ForFlags(WideEvents.Top).Subscribe(unsigned.Add);

            stream.OnNext(0x80000000UL);
            stream.OnNext(1UL << 63);
            stream.OnNext(0x8000000080000000UL);

            Assert.That(signed, Is.EqualTo(new[] { 0x80000000UL, 0x8000000080000000UL }));
            Assert.That(unsigned, Is.EqualTo(new[] { 1UL << 63, 0x8000000080000000UL }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectsZeroMaskAndMissingFlagsAttributeWhenBuildingThePath(bool fromPath)
        {
            using var stream = new EventStream<ulong>();
            var path = stream.Where(_ => true);

            Assert.Throws<ArgumentException>(() =>
            {
                if (fromPath) path.ForFlags(GeoEvents.None);
                else stream.ForFlags(GeoEvents.None);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                if (fromPath) path.ForFlags(PlainEvent.Shape);
                else stream.ForFlags(PlainEvent.Shape);
            });

            Assert.That(stream.NodeCount, Is.Zero);
        }

        // Sharing and ordering
        [Test]
        public void EqualBitsShareAcrossEnumTypesAndDifferentMasksOrParentsRemainSeparate()
        {
            using var stream = new EventStream<ulong>();
            var shape = stream.ForFlags(GeoEvents.Shape);
            Assert.That(stream.NodeCount, Is.Zero);

            shape.Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            stream.ForFlags(GeoEvents.Shape).Subscribe(_ => { });
            stream.ForFlags(RouteEvents.Shape).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            stream.ForFlags(GeoEvents.Location).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            shape.ForFlags(RouteEvents.Shape).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FlagsEntriesAndSiblingPathsReceiveMatchingValues(bool flagsFirst)
        {
            using var stream = new EventStream<ulong>();
            var calls = new List<string>();

            if (!flagsFirst) stream.Where(_ => true).Subscribe(_ => calls.Add("where"));

            stream.ForFlags(GeoEvents.Location).Subscribe(_ => calls.Add("location"));

            if (flagsFirst) stream.Where(_ => true).Subscribe(_ => calls.Add("where"));

            stream.ForFlags(GeoEvents.Shape).Subscribe(_ => calls.Add("shape"));
            stream.ForFlags(RouteEvents.Location).Subscribe(_ => calls.Add("location-again"));
            stream.OnNext(3);

            var expected = flagsFirst
                ? new[] { "location", "location-again", "shape", "where" }
                : new[] { "where", "location", "location-again", "shape" };
            Assert.That(calls, Is.EquivalentTo(expected));
        }

        [Test]
        public void SubscribersAtTheSamePositionAndFlagsChildrenReceiveMatchingValues()
        {
            using var stream = new EventStream<ulong>();
            var calls = new List<string>();
            var shape = stream.ForFlags(GeoEvents.Shape);
            shape.ForFlags(GeoEvents.Location).Subscribe(_ => calls.Add("nested"));
            shape.Where(_ => true).Subscribe(_ => calls.Add("where"));
            shape.Subscribe(_ => calls.Add("shape"));
            stream.Subscribe(_ => calls.Add("root"));

            stream.OnNext(3);

            Assert.That(calls, Is.EquivalentTo(new[] { "root", "shape", "nested", "where" }));
        }

        [Test]
        public void OrderGroupsKeepSeparateFlagsContainersAndVisitInOrder()
        {
            using var stream = new EventStream<ulong>();
            var calls = new List<string>();
            stream.ForOrder(Phase.Render).ForFlags(GeoEvents.Shape).Subscribe(_ => calls.Add("render"));
            stream.ForOrder(Phase.Update).ForFlags(GeoEvents.Shape).Subscribe(_ => calls.Add("update"));
            stream.ForOrder(Phase.Update).ForFlags(RouteEvents.Shape).Subscribe(_ => calls.Add("update-again"));

            Assert.That(stream.NodeCount, Is.EqualTo(3));
            stream.OnNext(1);

            Assert.That(calls, Is.EqualTo(new[] { "update", "update-again", "render" }));
        }

        // Publication changes and cleanup
        [Test]
        public void FlagsContainerAddedDuringPublicationStartsOnTheNextValue()
        {
            using var stream = new EventStream<ulong>();
            var calls = new List<string>();
            var added = false;
            stream.Subscribe(_ =>
            {
                calls.Add("root");
                if (added) return;
                added = true;
                stream.ForFlags(GeoEvents.Shape).Subscribe(_ => calls.Add("flags"));
            });

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "root" }));
            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "root", "root", "flags" }));
        }

        [Test]
        public void NewEntriesAndSubscribersToAnUnvisitedEntryWaitUntilTheNextValue()
        {
            using var stream = new EventStream<ulong>();
            var calls = new List<string>();
            var added = false;
            stream.ForFlags(GeoEvents.Shape).Subscribe(_ =>
            {
                calls.Add("shape");
                if (added) return;
                added = true;
                stream.ForFlags(GeoEvents.Location).Subscribe(_ => calls.Add("late"));
                stream.ForFlags(GeoEvents.Shape | GeoEvents.Location).Subscribe(_ => calls.Add("new-entry"));
            });
            stream.ForFlags(GeoEvents.Location).Subscribe(_ => calls.Add("location"));

            stream.OnNext(3);
            Assert.That(calls, Is.EqualTo(new[] { "shape", "location" }));
            calls.Clear();
            stream.OnNext(3);
            Assert.That(calls, Is.EqualTo(new[] { "shape", "location", "late", "new-entry" }));
        }

        [Test]
        public void RemovingTheCurrentEntryAndTheNextEntryPreservesTheRemainingCursor()
        {
            using var stream = new EventStream<ulong>();
            var calls = new List<string>();
            IDisposable current = null;
            IDisposable next = null;
            current = stream.ForFlags(GeoEvents.Shape).Subscribe(_ =>
            {
                calls.Add("current");
                current.Dispose();
                next.Dispose();
            });
            next = stream.ForFlags(GeoEvents.Location).Subscribe(_ => calls.Add("removed"));
            stream.ForFlags(GeoEvents.Shape | GeoEvents.Location).Subscribe(_ => calls.Add("remaining"));

            stream.OnNext(3);
            stream.OnNext(3);

            Assert.That(calls, Is.EquivalentTo(new[] { "current", "removed", "remaining", "remaining" }));
            Assert.That(stream.NodeCount, Is.EqualTo(1));
        }

        [Test]
        public void RemovingASubscriberInTheSameEntryWaitsForTheRoot()
        {
            using var stream = new EventStream<ulong>();
            var calls = new List<string>();
            IDisposable later = null;
            stream.ForFlags(GeoEvents.Shape).Subscribe(_ =>
            {
                calls.Add("first");
                later.Dispose();
            });
            later = stream.ForFlags(RouteEvents.Shape).Subscribe(_ => calls.Add("removed"));

            stream.OnNext(1);

            Assert.That(calls, Is.EquivalentTo(new[] { "first", "removed" }));
            Assert.That(stream.NodeCount, Is.EqualTo(1));
        }

        [Test]
        public void RecreatedEntryWaitsUntilTheNextPublication()
        {
            using var stream = new EventStream<ulong>();
            var calls = new List<string>();
            var replaced = false;
            IDisposable old = null;
            stream.ForFlags(GeoEvents.Shape).Subscribe(_ =>
            {
                calls.Add("first");
                if (replaced) return;
                replaced = true;
                old.Dispose();
                stream.ForFlags(GeoEvents.Location).Subscribe(_ => calls.Add("replacement"));
            });
            old = stream.ForFlags(GeoEvents.Location).Subscribe(_ => calls.Add("old"));
            stream.ForFlags(GeoEvents.Shape | GeoEvents.Location).Subscribe(_ => calls.Add("last"));

            stream.OnNext(3);
            Assert.That(calls, Is.EquivalentTo(new[] { "first", "old", "last" }));
            calls.Clear();
            stream.OnNext(3);
            Assert.That(calls, Is.EquivalentTo(new[] { "first", "last", "replacement" }));
        }

        [Test]
        public void LastSubscriberPrunesEntriesAndContainerAndTheBaseStreamCanBeReused()
        {
            using var stream = new EventStream<ulong>();
            var first = stream.ForFlags(GeoEvents.Shape).Subscribe(_ => { });
            var duplicate = stream.ForFlags(RouteEvents.Shape).Subscribe(_ => { });
            var second = stream.ForFlags(GeoEvents.Location).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));

            first.Dispose();
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            duplicate.Dispose();
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            second.Dispose();
            Assert.That(stream.NodeCount, Is.Zero);
            Assert.That(stream.IsDisposed, Is.False);

            var values = new List<ulong>();
            stream.ForFlags(GeoEvents.Shape).Subscribe(values.Add);
            stream.OnNext(1);
            Assert.That(values, Is.EqualTo(new[] { 1UL }));
            Assert.That(stream.NodeCount, Is.EqualTo(1));
        }

        [Test]
        public void EmptyFlagsRootDisposesARelationAndRemovesItsUpstreamConnection()
        {
            using var parent = new EventStream<ulong>();
            using var branch = parent.ForFlags(GeoEvents.Shape).Branch();
            var subscription = branch.ForFlags(GeoEvents.Location).Subscribe(_ => { });
            Assert.That(parent.NodeCount, Is.EqualTo(2));
            Assert.That(branch.NodeCount, Is.EqualTo(1));

            subscription.Dispose();

            Assert.That(branch.IsDisposed, Is.True);
            Assert.That(branch.NodeCount, Is.Zero);
            Assert.That(parent.NodeCount, Is.Zero);
            Assert.That(parent.IsDisposed, Is.False);
        }

        // Termination and composition
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void CompletionErrorAndDisposalReachEveryEntryRegardlessOfItsMask(int ending)
        {
            using var stream = new EventStream<ulong>();
            var completed = new List<string>();
            var errors = new List<Exception>();
            var values = new List<ulong>();
            var failure = new InvalidOperationException("terminal");
            var first = (Handle)stream.ForFlags(GeoEvents.Shape)
                .Subscribe(values.Add, () => completed.Add("shape"), errors.Add);
            using var branch = stream.ForFlags(GeoEvents.Location).Branch();
            var second = (Handle)branch.ForFlags(WideEvents.Top)
                .Subscribe(values.Add, () => completed.Add("branch"), errors.Add);
            stream.OnNext(0);

            if (ending == 0) stream.OnCompleted();
            else if (ending == 1) stream.OnError(failure);
            else stream.Dispose();

            Assert.That(values, Is.Empty);
            Assert.That(completed, Is.EqualTo(ending == 0 ? new[] { "shape", "branch" } : Array.Empty<string>()));
            Assert.That(errors, Is.EqualTo(ending == 1 ? new[] { failure, failure } : Array.Empty<Exception>()));
            Assert.That(first.IsDisposed && second.IsDisposed && branch.IsDisposed && stream.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.Zero);
            Assert.That(branch.NodeCount, Is.Zero);
        }

        [Test]
        public void FlagsComposeWithWhereOrderTakeAndBranchOnBothSides()
        {
            using var source = new EventStream<ulong>();
            var calls = new List<string>();
            var checks = 0;
            var path = source.Where(value => value < 8).ForFlags(GeoEvents.Shape).Where(value =>
            {
                checks++;
                return value != 5;
            });
            using var taken = path.ForOrder(Phase.Render).Take(2);
            using var branch = taken.ForFlags(GeoEvents.Location).Branch();
            branch.Subscribe(value => calls.Add($"render:{value}"), () => calls.Add("complete"), null);
            path.ForOrder(Phase.Update).Subscribe(value => calls.Add($"update:{value}"));

            source.OnNext(8);
            source.OnNext(2);
            source.OnNext(5);
            source.OnNext(1);
            source.OnNext(3);
            source.OnNext(7);

            Assert.That(checks, Is.EqualTo(4));
            Assert.That(calls, Is.EqualTo(new[] { "update:1", "update:3", "render:3", "complete", "update:7" }));
            Assert.That(taken.IsDisposed && branch.IsDisposed, Is.True);
        }

        [Test]
        public void SkipBeforeAndAfterFlagsCountsOnlyValuesAtItsOwnPosition()
        {
            using var source = new EventStream<ulong>();
            using var before = source.Skip(1);
            using var after = before.ForFlags(GeoEvents.Shape).Skip(1);
            var values = new List<ulong>();
            after.ForFlags(GeoEvents.Location).Subscribe(values.Add);

            source.OnNext(3);
            source.OnNext(2);
            source.OnNext(1);
            source.OnNext(3);

            Assert.That(values, Is.EqualTo(new[] { 3UL }));
        }

        [Test]
        public void DebugBeforeAndAfterFlagsObservesItsOwnPassingValues()
        {
            using var source = new EventStream<ulong>();
            var values = new List<ulong>();
            source.Debug("before", LogLevel.Warning).ForFlags(GeoEvents.Shape)
                .Debug("after", LogLevel.Warning).Subscribe(values.Add);
            using var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                source.OnNext(2);
                source.OnNext(3);
            }
            finally
            {
                Console.SetOut(original);
            }

            Assert.That(values, Is.EqualTo(new[] { 3UL }));
            Assert.That(output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
                Is.EqualTo(new[] { "2: before", "3: before", "3: after" }));
        }

        [Test]
        public void EachFlagsOperationConsumesOnePathDepthAndOneGraphNode()
        {
            using var source = new EventStream<ulong>();
            var path = source.ForFlags(GeoEvents.Shape);
            for (var depth = 1; depth < 256; depth++) path = path.ForFlags(GeoEvents.Shape);

            var values = new List<ulong>();
            using var subscription = path.Subscribe(values.Add);
            Assert.That(path.Depth, Is.EqualTo(256));
            Assert.That(source.NodeCount, Is.EqualTo(256));
            Assert.Throws<InvalidOperationException>(() => path.ForFlags(GeoEvents.Shape));
            source.OnNext(1);

            Assert.That(values, Is.EqualTo(new[] { 1UL }));
        }

        // Cases
        [Flags]
        private enum GeoEvents
        {
            None = 0,
            Shape = 1,
            Location = 2,
            Sign = 1 << 31,
        }

        [Flags]
        private enum RouteEvents : byte { Shape = 1, Location = 2 }

        [Flags]
        private enum WideEvents : ulong { Top = 1UL << 63 }

        private enum PlainEvent { Shape = 1 }
        private enum Phase { Update, Render }
    }
}
