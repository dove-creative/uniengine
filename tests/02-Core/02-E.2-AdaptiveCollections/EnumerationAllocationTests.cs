using System;
using System.Runtime.CompilerServices;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EnumerationAllocationTests
    {
        // Content
        // A1: Measure only direct traversal after compilation and source initialization.
        [TestCase(false, 0)]
        [TestCase(false, 16)]
        [TestCase(true, 0)]
        [TestCase(true, 16)]
        public void DirectEnumerationAllocatesNoManagedMemory(bool deferred, int count)
        {
            var collection = new AdaptiveCollection<int>(deferred);
            for (var index = 1; index <= count; index++) collection.Add(index);
            Sum(collection, 2000);

            var before = GC.GetAllocatedBytesForCurrentThread();
            var sum = Sum(collection, 10000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(sum, Is.EqualTo((long)count * (count + 1) / 2 * 10000));
            Assert.That(allocated, Is.Zero);
            TestContext.WriteLine($"Direct enumeration: deferred={deferred}, count={count}, allocated={allocated} bytes.");
        }

        // A1: A previously created selector does not introduce per-enumeration allocations.
        [Test]
        public void ForEachReusesItsSelectorWithoutAllocatingAnEnumerator()
        {
            var collection = new AdaptiveCollection<int> { 1, 2, 3 };
            var total = 0;
            var selector = new Action<int>(value => total += value);
            Apply(collection, selector, 2000);
            total = 0;

            var before = GC.GetAllocatedBytesForCurrentThread();
            Apply(collection, selector, 10000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(total, Is.EqualTo(60000));
            Assert.That(allocated, Is.Zero);
            TestContext.WriteLine($"ForEach with a reused selector: allocated={allocated} bytes.");
        }

        // Warm and measure the same loop separately from test assertions.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Apply(AdaptiveCollection<int> collection, Action<int> selector, int repetitions)
        {
            for (var iteration = 0; iteration < repetitions; iteration++) collection.ForEach(selector);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long Sum(AdaptiveCollection<int> collection, int repetitions)
        {
            var total = 0L;
            for (var iteration = 0; iteration < repetitions; iteration++)
            {
                foreach (var item in collection) total += item;
            }
            return total;
        }
    }
}
