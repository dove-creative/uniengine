using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class AdaptiveHashSetTests
    {
        // Content
        [TestCase(false)]
        [TestCase(true)]
        public void EmptyAndPopulatedSetsPreserveUniquenessAndNulls(bool deferred)
        {
            var collection = new AdaptiveHashSet<string>(deferred);
            Assert.That(Read(collection), Is.Empty);
            collection.AddRange(new[] { null, "first", "first", "last" });
            Assert.That(Read(collection), Is.EquivalentTo(new[] { null, "first", "last" }));
            Assert.That(Read(collection), Is.EquivalentTo(new[] { null, "first", "last" }));
            Assert.That(collection.Count, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EnumerationStartsLazilyAndRejectsNestedAcquisition(bool deferred)
        {
            var collection = new AdaptiveHashSet<int>(deferred) { 1 };
            var unused = collection.GetEnumerator();
            var outer = collection.GetEnumerator();
            unused.Dispose();
            collection.Add(2);
            Assert.That(unused.MoveNext(), Is.False);
            Assert.That(outer.MoveNext(), Is.True);
            var seen = new List<int> { outer.Current };
            var nested = collection.GetEnumerator();
            Assert.Throws<InvalidOperationException>(() => nested.MoveNext());
            nested.Dispose();
            collection.Add(3);

            while (outer.MoveNext()) seen.Add(outer.Current);

            outer.Dispose();
            Assert.That(seen, Is.EquivalentTo(deferred ? new[] { 1, 2 } : new[] { 1, 2, 3 }));
            Assert.That(Read(collection), Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdditionsFollowTheUpdateModeAcrossMultipleBatches(bool deferred)
        {
            var collection = new AdaptiveHashSet<int>(deferred) { 0 };
            var seen = new List<int>();
            foreach (var item in collection)
            {
                seen.Add(item);

                if (item >= 3) continue;

                collection.Add(item + 1);
                collection.Add(item + 1);
                Assert.That(collection.Contains(item + 1), Is.True);
                Assert.That(collection.Count, Is.EqualTo(item + 2));
            }

            Assert.That(seen, Is.EqualTo(deferred ? new[] { 0 } : new[] { 0, 1, 2, 3 }));
            Assert.That(Read(collection), Is.EquivalentTo(deferred ? new[] { 0, 1 } : new[] { 0, 1, 2, 3 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingFutureValuesSkipsThemWithoutInvalidatingTheCursor(bool deferred)
        {
            var collection = new AdaptiveHashSet<int>(deferred) { 1, 2, 3 };
            var cursor = collection.GetEnumerator();
            Assert.That(cursor.MoveNext(), Is.True);

            var first = cursor.Current;
            for (var value = 1; value <= 3; value++)
            {
                if (value == first) continue;

                Assert.That(collection.Remove(value), Is.True);
                Assert.That(collection.Remove(value), Is.False);
                Assert.That(collection.Contains(value), Is.False);
            }

            Assert.That(collection.Count, Is.EqualTo(1));
            Assert.That(cursor.MoveNext(), Is.False);
            cursor.Dispose();
            Assert.That(Read(collection), Is.EqualTo(new[] { first }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingAndReaddingTheCurrentValueCreatesANewVisit(bool deferred)
        {
            var collection = new AdaptiveHashSet<int>(deferred) { 1 };
            var visits = 0;
            foreach (var item in collection)
            {
                visits++;

                if (visits != 1) continue;

                Assert.That(collection.Remove(item), Is.True);
                collection.Add(item);
                Assert.That(collection.Count, Is.EqualTo(1));
                Assert.That(collection.Contains(item), Is.True);
            }

            Assert.That(visits, Is.EqualTo(deferred ? 1 : 2));
            Assert.That(Read(collection), Is.EqualTo(new[] { 1 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PendingAdditionsCanBeCancelled(bool deferred)
        {
            var collection = new AdaptiveHashSet<int>(deferred) { 1 };
            foreach (var item in collection)
            {
                collection.Add(2);
                Assert.That(collection.Remove(2), Is.True);
                Assert.That(collection.Remove(2), Is.False);
                Assert.That(collection.Contains(2), Is.False);
                Assert.That(collection.Count, Is.EqualTo(1));
            }

            Assert.That(Read(collection), Is.EqualTo(new[] { 1 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearStopsTheCurrentEnumerationAndPreservesLaterAdditions(bool deferred)
        {
            var collection = new AdaptiveHashSet<int>(deferred) { 1, 2 };
            var visits = 0;
            foreach (var item in collection)
            {
                visits++;
                collection.Add(3);
                collection.Clear();
                collection.Add(4);
                Assert.That(collection.Count, Is.EqualTo(1));
            }

            Assert.That(visits, Is.EqualTo(1));
            Assert.That(Read(collection), Is.EqualTo(new[] { 4 }));
        }

        [Test]
        public void ClearDuringAReusedBatchStopsThatBatch()
        {
            var collection = new AdaptiveHashSet<int>(false) { 0 };
            var seen = new List<int>();
            foreach (var item in collection)
            {
                seen.Add(item);

                if (item == 0)
                {
                    collection.Add(1);
                    continue;
                }

                collection.Add(2);
                collection.Clear();
                collection.Add(9);
            }

            Assert.That(seen, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(Read(collection), Is.EqualTo(new[] { 9 }));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void EarlyExitAppliesPendingChangesAndReleasesTheCursor(bool deferred, bool fail)
        {
            var collection = new AdaptiveHashSet<int>(deferred) { 1 };
            try
            {
                foreach (var item in collection)
                {
                    collection.Remove(item);
                    collection.Add(2);

                    if (fail) throw new ApplicationException("body");

                    break;
                }
            }
            catch (ApplicationException)
            {
                Assert.That(fail, Is.True);
            }

            Assert.That(Read(collection), Is.EqualTo(new[] { 2 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedPendingCommitStillReleasesTheEnumeration(bool deferred)
        {
            var original = new FaultItem(1);
            var pending = new FaultItem(2);
            var collection = new AdaptiveHashSet<FaultItem>(deferred) { original };
            var cursor = collection.GetEnumerator();
            Assert.That(cursor.MoveNext(), Is.True);
            collection.Add(pending);
            pending.FailHashing = true;

            Assert.Throws<InvalidOperationException>(() => cursor.Dispose());

            pending.FailHashing = false;
            Assert.That(collection.Count, Is.EqualTo(1));
            Assert.That(collection.Contains(pending), Is.False);
            Assert.That(Read(collection), Is.EqualTo(new[] { original }));
            collection.Add(pending);
            Assert.That(Read(collection), Is.EquivalentTo(new[] { original, pending }));
        }

        [Test]
        public void CopiesShareTheCursorAndStaleCopiesCannotEndANewEnumeration()
        {
            var collection = new AdaptiveHashSet<int> { 1, 2, 3 };
            var first = collection.GetEnumerator();
            Assert.That(first.MoveNext(), Is.True);
            var firstValue = first.Current;
            var copy = first;
            Assert.That(copy.MoveNext(), Is.True);
            Assert.That(copy.Current, Is.Not.EqualTo(firstValue));
            Assert.That(first.Current, Is.EqualTo(firstValue));
            var stale = first;
            copy.Dispose();
            var current = collection.GetEnumerator();
            Assert.That(current.MoveNext(), Is.True);
            first.Dispose();
            Assert.Throws<InvalidOperationException>(() => stale.MoveNext());
            Assert.That(current.MoveNext(), Is.True);
            current.Dispose();
            Assert.That(Read(collection), Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void InterfaceEnumerationAndForEachKeepCleanupSemantics()
        {
            var collection = new AdaptiveHashSet<int> { 1 };
            using (var cursor = ((IEnumerable<int>)collection).GetEnumerator())
            {
                Assert.That(cursor.MoveNext(), Is.True);
                collection.Add(2);
                Assert.Throws<NotSupportedException>(() => cursor.Reset());
                Assert.Throws<InvalidOperationException>(() => collection.CopyTo(new int[2], 0));
            }

            var untyped = ((IEnumerable)collection).GetEnumerator();
            var values = new List<object>();
            try
            {
                while (untyped.MoveNext()) values.Add(untyped.Current);
            }
            finally
            {
                ((IDisposable)untyped).Dispose();
            }

            Assert.That(values, Is.EquivalentTo(new[] { 1, 2 }));

            Assert.Throws<ApplicationException>(() => collection.ForEach(value =>
            {
                collection.Add(3);
                throw new ApplicationException("selector");
            }));

            var copied = new int[3];
            collection.CopyTo(copied, 0);
            Assert.That(copied, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [TestCase(false, 0)]
        [TestCase(false, 16)]
        [TestCase(true, 0)]
        [TestCase(true, 16)]
        public void DirectEnumerationAllocatesNoManagedMemory(bool deferred, int count)
        {
            var collection = new AdaptiveHashSet<int>(deferred);
            for (var index = 1; index <= count; index++) collection.Add(index);

            Sum(collection, 2000);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sum = Sum(collection, 10000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sum, Is.EqualTo((long)count * (count + 1) / 2 * 10000));
            Assert.That(allocated, Is.Zero);
            TestContext.WriteLine(
                $"HashSet direct enumeration: deferred={deferred}, count={count}, allocated={allocated} bytes.");
        }

        // A past removal keeps the marker set's buckets, so clean passes must still skip hashing.
        [TestCase(false)]
        [TestCase(true)]
        public void DirectEnumerationAfterRemovalAllocatesNoManagedMemory(bool deferred)
        {
            var collection = new AdaptiveHashSet<PlainValue>(deferred);
            for (var index = 1; index <= 4; index++) collection.Add(new PlainValue(index));

            foreach (var item in collection) collection.Remove(item);

            for (var index = 1; index <= 4; index++) collection.Add(new PlainValue(index));

            Sum(collection, 2000);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sum = Sum(collection, 10000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sum, Is.EqualTo(10L * 10000));
            Assert.That(allocated, Is.Zero);
            TestContext.WriteLine(
                $"HashSet enumeration after removal: deferred={deferred}, allocated={allocated} bytes.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedMutationsReusePendingAndBatchStorage(bool deferred)
        {
            var collection = new AdaptiveHashSet<int>(deferred);
            Cycle(collection, 2000);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sum = Cycle(collection, 10000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(sum, Is.EqualTo(deferred ? 0 : 60000));
            Assert.That(allocated, Is.Zero);
            Assert.That(collection.Count, Is.EqualTo(deferred ? 1 : 0));
            TestContext.WriteLine($"HashSet repeated mutation: deferred={deferred}, allocated={allocated} bytes.");
        }

        private static List<T> Read<T>(AdaptiveHashSet<T> collection)
        {
            var values = new List<T>();
            foreach (var item in collection) values.Add(item);

            return values;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long Sum(AdaptiveHashSet<int> collection, int repetitions)
        {
            var total = 0L;
            for (var iteration = 0; iteration < repetitions; iteration++)
            {
                foreach (var item in collection) total += item;
            }

            return total;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long Sum(AdaptiveHashSet<PlainValue> collection, int repetitions)
        {
            var total = 0L;
            for (var iteration = 0; iteration < repetitions; iteration++)
            {
                foreach (var item in collection) total += item.Value;
            }

            return total;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Cycle(AdaptiveHashSet<int> collection, int repetitions)
        {
            var total = 0;
            for (var iteration = 0; iteration < repetitions; iteration++)
            {
                collection.Clear();
                collection.Add(0);

                foreach (var item in collection)
                {
                    total += item;
                    collection.Remove(item);

                    if (item < 3) collection.Add(item + 1);
                }
            }

            return total;
        }

        private class FaultItem
        {
            public bool FailHashing { get; set; }

            private readonly int _value;


            public FaultItem(int value) => _value = value;
            public override bool Equals(object obj) => obj is FaultItem other && _value == other._value;
            public override int GetHashCode() => FailHashing
                ? throw new InvalidOperationException("hash failure")
                : _value;
        }

        // Relies on the default value-type equality, which boxes whenever the value is hashed.
        private struct PlainValue
        {
            public readonly int Value;


            public PlainValue(int value) => Value = value;
        }
    }
}
