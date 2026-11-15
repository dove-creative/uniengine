using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class AdaptiveDictionaryTests
    {
        // Content --------------------
        // Mutation and lifetime
        // D2: Logically removed keys return the default out value before pending changes are committed.
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void RemovedKeysReturnDefaultValuesDuringIteration(bool deferred, bool replaceFirst)
        {
            var dictionary = new AdaptiveDictionary<int, string>(deferred) { { 1, "original" }, { 2, "retained" } };
            var cursor = dictionary.GetEnumerator();
            try
            {
                Assert.That(cursor.MoveNext(), Is.True);
                if (replaceFirst) dictionary[1] = "replacement";
                Assert.That(dictionary.Remove(1), Is.True);
                Assert.That(dictionary.TryGetValue(1, out var removed), Is.False);
                Assert.That(removed, Is.Null);
                Assert.That(dictionary.ContainsKey(1), Is.False);
                Assert.That(dictionary.Count, Is.EqualTo(1));
                Assert.That(dictionary.TryGetValue(2, out var retained), Is.True);
                Assert.That(retained, Is.EqualTo("retained"));
            }
            finally
            {
                cursor.Dispose();
            }

            Assert.That(dictionary.TryGetValue(1, out var missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(dictionary.Keys, Is.EquivalentTo(new[] { 2 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdditionAndReplacementFollowTheSelectedMode(bool deferred)
        {
            var dictionary = new AdaptiveDictionary<int, int>(deferred) { { 1, 10 } };
            var seen = new List<int>();
            foreach (var item in dictionary)
            {
                seen.Add(item.Value);

                if (item.Value != 10) continue;

                dictionary[1] = 20;
                dictionary.Add(2, 30);
                Assert.That(dictionary.Count, Is.EqualTo(2));
                Assert.That(dictionary[1], Is.EqualTo(20));
            }

            Assert.That(seen, Is.EquivalentTo(deferred ? new[] { 10 } : new[] { 10, 20, 30 }));
            Assert.That(dictionary[1], Is.EqualTo(20));
            Assert.That(dictionary[2], Is.EqualTo(30));
        }

        [Test]
        public void ImmediateBatchesSkipRemovedEntriesAndVisitReplacements()
        {
            var dictionary = new AdaptiveDictionary<int, int>(false) { { 0, 0 } };
            var seen = new List<int>();
            foreach (var item in dictionary)
            {
                seen.Add(item.Value);

                if (item.Key == 0)
                {
                    dictionary.Add(1, 10);
                    dictionary.Add(2, 20);
                }
                else if (item.Value == 10 || item.Value == 20)
                {
                    var otherKey = item.Key == 1 ? 2 : 1;
                    dictionary.Remove(otherKey);
                    dictionary.Add(otherKey, 100);
                    dictionary.Remove(item.Key);
                }
            }

            Assert.That(seen.Count, Is.EqualTo(3));
            Assert.That(seen[0], Is.Zero);
            Assert.That(seen[2], Is.EqualTo(100));
            Assert.That(dictionary.Count, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingPendingReplacementDoesNotRestoreTheOriginal(bool deferred)
        {
            var dictionary = new AdaptiveDictionary<int, int>(deferred) { { 1, 10 } };
            foreach (var item in dictionary)
            {
                dictionary[1] = 20;
                Assert.That(dictionary.Remove(1), Is.True);
                Assert.That(dictionary.ContainsKey(1), Is.False);
                Assert.That(dictionary.Count, Is.Zero);
            }

            Assert.That(dictionary.Count, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearStopsTheCurrentPassAndPreservesLaterAdditions(bool deferred)
        {
            var dictionary = new AdaptiveDictionary<int, int>(deferred) { { 1, 10 }, { 2, 20 } };
            var visits = 0;
            foreach (var item in dictionary)
            {
                visits++;
                dictionary.Add(3, 30);
                dictionary.Clear();
                dictionary.Add(4, 40);
            }

            Assert.That(visits, Is.EqualTo(1));
            Assert.That(dictionary.Count, Is.EqualTo(1));
            Assert.That(dictionary[4], Is.EqualTo(40));
            Assert.That(Sum(dictionary, 1), Is.EqualTo(40));
        }

        [Test]
        public void ClearDuringAnImmediateBatchStopsTheReusableBuffer()
        {
            var dictionary = new AdaptiveDictionary<int, int>(false) { { 0, 0 } };
            var visits = 0;
            foreach (var item in dictionary)
            {
                visits++;

                if (item.Key == 0)
                {
                    dictionary.Add(1, 10);
                    dictionary.Add(2, 20);
                }
                else
                {
                    dictionary.Clear();
                    dictionary.Add(3, 30);
                }
            }

            Assert.That(visits, Is.EqualTo(2));
            Assert.That(dictionary.Count, Is.EqualTo(1));
            Assert.That(Sum(dictionary, 1), Is.EqualTo(30));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EarlyDisposalAndCallbackFailureApplyPendingChanges(bool deferred)
        {
            var dictionary = new AdaptiveDictionary<int, int>(deferred) { { 1, 10 } };
            foreach (var item in dictionary)
            {
                dictionary.Add(2, 20);
                break;
            }

            Assert.That(dictionary.Count, Is.EqualTo(2));

            Assert.Throws<ApplicationException>(() => dictionary.ForEach((Action<int, int>)((key, value) =>
            {
                dictionary.Add(3, 30);
                throw new ApplicationException("callback");
            })));

            Assert.That(Sum(dictionary, 1), Is.EqualTo(60));
        }

        [Test]
        public void EnumerationIsLazyAndRejectsNestedAndStaleCursors()
        {
            var dictionary = new AdaptiveDictionary<int, int> { { 1, 10 }, { 2, 20 } };
            var unused = dictionary.GetEnumerator();
            unused.Dispose();
            var first = dictionary.GetEnumerator();
            Assert.That(first.MoveNext(), Is.True);
            var stale = first;
            var nested = dictionary.GetEnumerator();
            Assert.Throws<InvalidOperationException>(() => nested.MoveNext());
            Assert.That(first.MoveNext(), Is.True);
            first.Dispose();

            var next = dictionary.GetEnumerator();
            Assert.That(next.MoveNext(), Is.True);
            Assert.Throws<InvalidOperationException>(() => stale.MoveNext());
            stale.Dispose();
            Assert.That(next.MoveNext(), Is.True);
            next.Dispose();
            Assert.That(Sum(dictionary, 1), Is.EqualTo(30));
        }

        [Test]
        public void GenericAndLegacyInterfacesRemainUsable()
        {
            var dictionary = new AdaptiveDictionary<int, int> { { 1, 10 } };
            using (var enumerator = ((IEnumerable<KeyValuePair<int, int>>)dictionary).GetEnumerator())
            {
                Assert.That(enumerator.MoveNext(), Is.True);
                Assert.That(enumerator.Current, Is.EqualTo(new KeyValuePair<int, int>(1, 10)));
            }

            var legacy = ((IDictionary)dictionary).GetEnumerator();
            try
            {
                Assert.That(legacy.MoveNext(), Is.True);
                Assert.That(legacy.Key, Is.EqualTo(1));
                Assert.That(legacy.Value, Is.EqualTo(10));
                dictionary.Add(2, 20);
            }
            finally
            {
                ((IDisposable)legacy).Dispose();
            }

            Assert.That(Sum(dictionary, 1), Is.EqualTo(30));
        }

        // Allocation measurements exclude setup, assertions and reporting.
        [TestCase(false, 0)]
        [TestCase(false, 16)]
        [TestCase(true, 0)]
        [TestCase(true, 16)]
        public void DirectEnumerationAllocatesNoManagedMemory(bool deferred, int count)
        {
            var dictionary = new AdaptiveDictionary<int, int>(deferred);
            for (var index = 1; index <= count; index++) dictionary.Add(index, index);

            Sum(dictionary, 2000);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var total = Sum(dictionary, 10000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(total, Is.EqualTo((long)count * (count + 1) / 2 * 10000));
            Assert.That(allocated, Is.Zero);
            TestContext.WriteLine(
                $"Dictionary direct enumeration: deferred={deferred}, count={count}, allocated={allocated} bytes.");
        }

        // A past removal keeps the marker set's buckets, so clean passes must still skip hashing.
        [TestCase(false)]
        [TestCase(true)]
        public void DirectEnumerationAfterRemovalAllocatesNoManagedMemory(bool deferred)
        {
            var dictionary = new AdaptiveDictionary<PlainKey, int>(deferred);
            for (var index = 1; index <= 4; index++) dictionary.Add(new PlainKey(index), index);

            foreach (var item in dictionary) dictionary.Remove(item.Key);

            for (var index = 1; index <= 4; index++) dictionary.Add(new PlainKey(index), index);

            Sum(dictionary, 2000);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var total = Sum(dictionary, 10000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(total, Is.EqualTo(10L * 10000));
            Assert.That(allocated, Is.Zero);
            TestContext.WriteLine(
                $"Dictionary enumeration after removal: deferred={deferred}, allocated={allocated} bytes.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedMutationsReuseStorageWithoutAllocating(bool deferred)
        {
            var dictionary = new AdaptiveDictionary<int, int>(deferred);
            Mutate(dictionary, 2000);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var total = Mutate(dictionary, 10000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(total, Is.EqualTo((deferred ? 1L : 6L) * 10000));
            Assert.That(allocated, Is.Zero);
            Assert.That(dictionary.Count, Is.EqualTo(1));
            TestContext.WriteLine($"Dictionary repeated mutations: deferred={deferred}, allocated={allocated} bytes.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long Sum<TKey>(AdaptiveDictionary<TKey, int> dictionary, int repetitions)
        {
            var total = 0L;
            for (var iteration = 0; iteration < repetitions; iteration++)
            {
                foreach (var item in dictionary) total += item.Value;
            }

            return total;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long Mutate(AdaptiveDictionary<int, int> dictionary, int repetitions)
        {
            var total = 0L;
            for (var iteration = 0; iteration < repetitions; iteration++)
            {
                dictionary.Clear();
                dictionary.Add(1, 1);

                foreach (var item in dictionary)
                {
                    total += item.Value;

                    if (item.Key == 3) continue;

                    dictionary.Remove(item.Key);
                    dictionary.Add(item.Key + 1, item.Value + 1);
                }
            }

            return total;
        }

        // Relies on the default value-type equality, which boxes whenever the key is hashed.
        private struct PlainKey
        {
            public readonly int Value;


            public PlainKey(int value) => Value = value;
        }
    }
}
