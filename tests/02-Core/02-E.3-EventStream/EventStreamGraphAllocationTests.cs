using System;
using System.Runtime.CompilerServices;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;
using Handle = Blackthunder.UniEngine.Core.Handle;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamGraphAllocationTests
    {
        // Content --------------------
        // First and repeated publication
        [TestCase(1, true, 3)]
        [TestCase(-1, true, 1)]
        [TestCase(1, false, 0)]
        public void FirstAndRepeatedPublicationsAllocateNoManagedMemory(int value, bool enabled, int expectedCalls)
        {
            const int repetitions = 10000;
            using var warm = new Graph(enabled);
            Publish(warm.Stream, value, 3000);

            // Measure a different graph before it has received any values.
            using var measured = new Graph(enabled);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Publish(measured.Stream, value, 1);
            var firstAllocated = GC.GetAllocatedBytesForCurrentThread() - before;

            before = GC.GetAllocatedBytesForCurrentThread();
            Publish(measured.Stream, value, repetitions);
            var repeatedAllocated = GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine($"EventStream graph: first={firstAllocated}, repeated={repeatedAllocated} bytes.");
            Assert.That(measured.Sum, Is.EqualTo(expectedCalls * value * (repetitions + 1)));
            Assert.That(measured.PredicateCalls, Is.EqualTo(enabled ? repetitions + 1 : 0));
            Assert.That(firstAllocated, Is.Zero);
            Assert.That(repeatedAllocated, Is.Zero);
        }

        [Test]
        public void FinalTakePublicationCompletesAndDisposesItsBranchWithoutAllocating()
        {
            using var warm = new EventStream<int>();
            MeasureFinalPublications(warm, 3000);

            using var measured = new EventStream<int>();
            var firstAllocated = MeasureFinalPublications(measured, 1);
            var repeatedAllocated = MeasureFinalPublications(measured, 10000);

            TestContext.WriteLine($"Take(1).Branch(): first={firstAllocated}, repeated={repeatedAllocated} bytes.");
            Assert.That(firstAllocated, Is.Zero);
            Assert.That(repeatedAllocated, Is.Zero);
        }

        // Helpers
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasureFinalPublications(EventStream<int> stream, int repetitions)
        {
            var allocated = 0L;
            var deliveries = 0;
            var completionsAfterValue = 0;
            var cleanups = 0;
            for (var index = 0; index < repetitions; index++)
            {
                // Each publication needs a fresh branch because its first value completes it.
                using var branch = stream.Take(1).Branch();
                var lastValue = 0;
                var handle = (Handle)branch.Subscribe(
                    value => { lastValue = value; deliveries++; },
                    () => { if (lastValue == 7) completionsAfterValue++; }, null);
                var before = GC.GetAllocatedBytesForCurrentThread();
                stream.OnNext(7);
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;

                if (handle.IsDisposed && branch.IsDisposed && stream.NodeCount == 0) cleanups++;
            }

            Assert.That(deliveries, Is.EqualTo(repetitions));
            Assert.That(completionsAfterValue, Is.EqualTo(repetitions));
            Assert.That(cleanups, Is.EqualTo(repetitions));
            return allocated;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Publish(EventStream<int> stream, int value, int repetitions)
        {
            for (var index = 0; index < repetitions; index++) stream.OnNext(value);
        }

        private class Graph : IDisposable
        {
            public EventStream<int> Stream { get; } = new();
            public int Sum { get; private set; }
            public int PredicateCalls { get; private set; }

            private readonly EventStream<int> _branch;

            public Graph(bool enabled)
            {
                var positive = new Filter<int>("positive", value => { PredicateCalls++; return value > 0; });
                var path = Stream.Where(positive);
                Stream.Subscribe(Record);
                path.Take(int.MaxValue).Subscribe(Record);
                _branch = path.Branch();
                _branch.Subscribe(Record);
                Stream.Enabled = enabled;
            }

            private void Record(int value) => Sum += value;

            public void Dispose()
            {
                _branch.Dispose();
                Stream.Dispose();
            }
        }
    }
}
