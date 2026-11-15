using System;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Core;
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

                    return CreateBase(
                        registration,
                        payload,
                        static (counter, value) =>
                        {
                            counter.Record(value.Value);
                            counter.LastReference = value;
                        },
                        payload);
                }

                return CreateBase(
                    registration,
                    new ValuePayload(7),
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
                var stream = new EventStream<int>();
                Action<int> callback = counter.Record;
                stream.ForOrder(UpdateOrder.Before).Subscribe(callback);
                stream.ForOrder(UpdateOrder.After).Subscribe(callback + callback);

                if (fillMiddleGroup) stream.ForOrder(UpdateOrder.Update).Subscribe(callback);

                var expectedCalls = fillMiddleGroup ? 4 : 3;
                return new Publication(counter, () => stream.OnNext(7), expectedCalls, expectedCalls * 7, stream);
            });
        }

        [TestCase(1, true, 1)]
        [TestCase(2, false, 0)]
        [TestCase(0, true, 0)]
        [TestCase(1, false, 1)]
        public void FirstAndRepeatedFiltersAllocateNoManagedMemory(int target, bool addUnfiltered, int matched)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var stream = new EventStream<int>();
                stream.Where(value => value == target).Subscribe(counter.Record);

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

                var where = source.Where(value =>
                {
                    counter.PredicateCalls++;
                    return value > 0;
                });

                var skip = where.Skip(0);
                var take = skip.Take(int.MaxValue);
                take.Subscribe(counter.Record);
                var input = matches ? 7 : 0;

                return new Publication(counter, () => source.OnNext(input), matches ? 1 : 0, input, source)
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

                return new Publication(
                    counter,
                    () =>
                    {
                        left.OnNext(3);
                        right.OnNext(4);
                    },
                    2,
                    7,
                    left,
                    right,
                    merged);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisabledPublicationsAllocateNoManagedMemory(bool ordered)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var stream = new EventStream<int>();

                Func<int, bool> predicate = _ =>
                {
                    counter.PredicateCalls++;
                    return true;
                };

                var path = ordered ? stream.ForOrder(UpdateOrder.Before).Where(predicate) : stream.Where(predicate);
                path.Subscribe(counter.Record);
                stream.Enabled = false;
                return new Publication(counter, () => stream.OnNext(7), 0, 0, stream);
            });
        }

        [TestCase(0, 0)]
        [TestCase(1, 2)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 0)]
        public void FirstAndRepeatedFlagsPublicationsAllocateNoManagedMemory(int value, int expectedCalls)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var stream = new EventStream<ulong>();
                Action<ulong> record = bits => counter.Record((int)bits);
                stream.ForFlags(EventKinds.First).Subscribe(record);
                stream.ForFlags(EventKinds.Second).Subscribe(record);
                stream.ForFlags(EventKinds.First | EventKinds.Second).Subscribe(record);

                return new Publication(
                    counter, () => stream.OnNext((ulong)value), expectedCalls, expectedCalls * value, stream);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstAndRepeatedComposedFlagsPublicationsAllocateNoManagedMemory(bool matches)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var stream = new EventStream<ulong>();
                var branch = stream.Where(_ => true).ForFlags(WideEventKinds.Top)
                    .ForOrder(UpdateOrder.Update).Skip(0).Take(int.MaxValue).Branch();
                branch.ForFlags(EventKinds.First).Subscribe(_ => counter.Record(1));
                var value = matches ? (1UL << 63) | 1UL : 1UL;
                var expected = matches ? 1 : 0;

                return new Publication(counter, () => stream.OnNext(value), expected, expected, stream, branch);
            });
        }

        [TestCase(1, 1)]
        [TestCase(2, 0)]
        [TestCase(0, 0)]
        public void FirstAndRepeatedWhenPublicationsAllocateNoManagedMemory(int value, int expectedCalls)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var stream = new EventStream<int>();
                stream.When(1).Subscribe(counter.Record);
                stream.When(3).Subscribe(counter.Record);
                return new Publication(
                    counter, () => stream.OnNext(value), expectedCalls, expectedCalls * value, stream);
            });
        }

        [TestCase(null, null, 1)]
        [TestCase(null, 1, 0)]
        [TestCase(1, null, 0)]
        [TestCase(1, 1, 1)]
        [TestCase(1, 2, 0)]
        public void FirstAndRepeatedNullableWhenPublicationsAllocateNoManagedMemory(
            int? target, int? value, int expectedCalls)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var stream = new EventStream<int?>();
                stream.When(target).Subscribe(_ => counter.Record(1));
                return new Publication(counter, () => stream.OnNext(value), expectedCalls, expectedCalls, stream);
            });
        }

        [TestCase(null, null, 1)]
        [TestCase(null, "value", 0)]
        [TestCase("value", null, 0)]
        [TestCase("value", "value", 1)]
        [TestCase("value", "other", 0)]
        public void FirstAndRepeatedReferenceWhenPublicationsAllocateNoManagedMemory(
            string target, string value, int expectedCalls)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var stream = new EventStream<string>();
                stream.When(target).Subscribe(_ => counter.Record(1));
                return new Publication(counter, () => stream.OnNext(value), expectedCalls, expectedCalls, stream);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstAndRepeatedComposedWhenPublicationsAllocateNoManagedMemory(bool matches)
        {
            Verify(() =>
            {
                var counter = new Counter();
                var stream = new EventStream<ulong>();
                var branch = stream.Where(_ => true).When(3).ForFlags(EventKinds.First)
                    .ForOrder(UpdateOrder.Update).Skip(0).Take(int.MaxValue).Branch();
                branch.When(3).Subscribe(_ => counter.Record(1));
                var value = matches ? 3UL : 1UL;
                var expected = matches ? 1 : 0;
                return new Publication(counter, () => stream.OnNext(value), expected, expected, stream, branch);
            });
        }

        // Helpers
        private static Publication CreateBase<T>(
            int registration,
            T payload,
            Action<Counter, T> record,
            object expectedReference = null)
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
            Assert.That(
                measured.Counter.PredicateCalls,
                Is.EqualTo(measured.PredicateCallsPerPublication * (repetitions + 1)));
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

        [Flags]
        private enum EventKinds { First = 1, Second = 2 }

        [Flags]
        private enum WideEventKinds : ulong { Top = 1UL << 63 }

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

            public Publication(
                Counter counter,
                Action invoke,
                int callsPerPublication,
                int sumPerPublication,
                params IDisposable[] owned)
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
