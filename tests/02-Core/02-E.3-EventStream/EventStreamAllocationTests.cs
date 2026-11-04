using System;
using System.Runtime.CompilerServices;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamAllocationTests
    {
        // Content --------------------
        // Allocation
        [Test]
        public void FirstAndRepeatedBasePublicationsAllocateNoManagedMemory(
            [Values(0, 1, 2, 3)] int registration,
            [Values(false, true)] bool referencePayload)
        {
            Verify(() =>
            {
                if (referencePayload)
                {
                    var payload = new ReferencePayload(7);
                    return CreateBase(registration, payload, static (counter, value) =>
                    {
                        counter.Record(value.Value);
                        counter.LastReference = value;
                    }, payload);
                }

                return CreateBase(registration, new ValuePayload(7),
                    static (counter, value) => counter.Record(value.Value));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstAndRepeatedOrderedPublicationsAllocateNoManagedMemory(bool fillMiddleGroup)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var stream = new EventStream<int, UpdateOrder>();
                Action<int> callback = counter.Record;
                stream.Subscribe(UpdateOrder.Before, callback);
                stream.Subscribe(UpdateOrder.After, callback + callback);
                if (fillMiddleGroup) stream.Subscribe(UpdateOrder.Update, callback);
                var expectedCalls = fillMiddleGroup ? 4 : 3;
                return new Publication(counter, () => stream.OnNext(7), expectedCalls, expectedCalls * 7, stream);
            });
        }

        [TestCase(1, true, 1)]
        [TestCase(2, false, 0)]
        [TestCase(0, true, 0)]
        [TestCase(1, false, 1)]
        public void FirstAndRepeatedMaskFiltersAllocateNoManagedMemory(int mask, bool addUnfiltered, int matched)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var stream = new EventStream<int>();
                stream.Where(new Filter<int>("mask", value => (mask & value) != 0)).Subscribe(counter.Record);
                if (addUnfiltered) stream.Subscribe(counter.Record);
                var expected = matched + (addUnfiltered ? 1 : 0);
                return new Publication(counter, () => stream.OnNext(1), expected, expected, stream);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstAndRepeatedFilterChainsAllocateNoManagedMemory(bool matches)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var source = new EventStream<int>();
                var where = source.Where(new Filter<int>("positive", value =>
                {
                    counter.PredicateCalls++;
                    return value > 0;
                }));
                var skip = where.Skip(0);
                var take = skip.Take(int.MaxValue);
                take.Subscribe(counter.Record);
                var input = matches ? 7 : 0;
                return new Publication(counter, () => source.OnNext(input), matches ? 1 : 0, input,
                    source)
                {
                    PredicateCallsPerPublication = 1,
                };
            });
        }

        [Test]
        public void FirstAndRepeatedMergedPublicationsAllocateNoManagedMemory()
        {
            Verify(() =>
            {
                var counter = new Counter();
                var left = new EventStream<int>();
                var right = new EventStream<int>();
                var merged = left.Merge(right);
                merged.Subscribe(counter.Record);
                return new Publication(counter, () =>
                {
                    left.OnNext(3);
                    right.OnNext(4);
                }, 2, 7, left, right, merged);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisabledPublicationsAllocateNoManagedMemory(bool ordered)
        {
            Verify(() =>
            {
                var counter = new Counter();
                EventStream<int> stream = ordered ? new EventStream<int, UpdateOrder>() : new EventStream<int>();
                stream.Where(new Filter<int>("disabled", _ =>
                {
                    counter.PredicateCalls++;
                    return true;
                })).Subscribe(counter.Record);
                stream.Enabled = false;
                return new Publication(counter, () => stream.OnNext(7), 0, 0, stream);
            });
        }

        // Helpers
        private static Publication CreateBase<T>(
            int registration, T payload, Action<Counter, T> record, object expectedReference = null)
        {
            var counter = new Counter();
            var stream = new EventStream<T>();
            Action<T> callback = value => record(counter, value);
            var expectedCalls = registration == 0 ? 0 : registration == 1 ? 1 : 3;
            switch (registration)
            {
                case 1:
                    stream.Subscribe(callback);
                    break;
                case 2:
                    stream.Subscribe(callback);
                    stream.Subscribe(callback);
                    stream.Subscribe(callback);
                    break;
                case 3:
                    stream.Subscribe(callback + callback + callback);
                    break;
            }

            return new Publication(counter, () => stream.OnNext(payload), expectedCalls, expectedCalls * 7, stream)
            {
                ExpectedReference = expectedCalls == 0 ? null : expectedReference,
            };
        }

        private static void Verify(Func<Publication> create)
        {
            const int repetitions = 10000;
            using var warm = create();
            Publish(warm.Invoke, 3000);

            // A different instance and fresh delegates must be ready before their first publication.
            using var measured = create();
            var before = GC.GetAllocatedBytesForCurrentThread();
            Publish(measured.Invoke, 1);
            var firstAllocated = GC.GetAllocatedBytesForCurrentThread() - before;

            before = GC.GetAllocatedBytesForCurrentThread();
            Publish(measured.Invoke, repetitions);
            var repeatedAllocated = GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine($"EventStream: first={firstAllocated}, repeated={repeatedAllocated} bytes.");
            Assert.That(measured.Counter.Calls, Is.EqualTo(measured.CallsPerPublication * (repetitions + 1)));
            Assert.That(measured.Counter.Sum, Is.EqualTo(measured.SumPerPublication * (repetitions + 1)));
            Assert.That(measured.Counter.PredicateCalls, Is.EqualTo(measured.PredicateCallsPerPublication * (repetitions + 1)));
            Assert.That(measured.Counter.LastReference, Is.SameAs(measured.ExpectedReference));
            Assert.That(firstAllocated, Is.Zero);
            Assert.That(repeatedAllocated, Is.Zero);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Publish(Action invoke, int repetitions)
        {
            for (var index = 0; index < repetitions; index++) invoke();
        }

        private class Counter
        {
            public int Calls;
            public int Sum;
            public int PredicateCalls;
            public object LastReference;

            public void Record(int value)
            {
                Calls++;
                Sum += value;
            }
        }

        private readonly struct ValuePayload
        {
            public int Value { get; }

            public ValuePayload(int value) => Value = value;
        }

        private class ReferencePayload
        {
            public int Value { get; }

            public ReferencePayload(int value) => Value = value;
        }

        private enum UpdateOrder
        {
            Before,
            Update,
            After,
            Empty,
        }

        private class Publication : IDisposable
        {
            public Counter Counter { get; }
            public Action Invoke { get; }
            public int CallsPerPublication { get; }
            public int SumPerPublication { get; }
            public int PredicateCallsPerPublication { get; set; }
            public object ExpectedReference { get; set; }

            private readonly IDisposable[] _owned;

            public Publication(Counter counter, Action invoke, int callsPerPublication, int sumPerPublication, params IDisposable[] owned)
            {
                Counter = counter;
                Invoke = invoke;
                CallsPerPublication = callsPerPublication;
                SumPerPublication = sumPerPublication;
                _owned = owned;
            }

            public void Dispose()
            {
                for (var index = _owned.Length - 1; index >= 0; index--) _owned[index].Dispose();
            }
        }
    }
}
