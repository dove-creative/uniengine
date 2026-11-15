using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class AdaptiveActionAllocationTests
    {
        // Content --------------------
        // Allocation
        [Test]
        public void FirstAndRepeatedInvocationsAllocateNoManagedMemory(
            [Values(0, 1, 2, 3)] int arity,
            [Values(0, 1, 2, 3)] int registration,
            [Values(false, true)] bool deferred)
        {
            var warm = CreateInvocation(arity, registration, deferred, new Counter());
            Invoke(warm, 2000);

            var counter = new Counter();
            var invoke = CreateInvocation(arity, registration, deferred, counter);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Invoke(invoke, 1);
            var firstAllocated = GC.GetAllocatedBytesForCurrentThread() - before;

            before = GC.GetAllocatedBytesForCurrentThread();
            Invoke(invoke, 10000);
            var repeatedAllocated = GC.GetAllocatedBytesForCurrentThread() - before;

            var perInvocation = arity < 2 ? 3 : arity == 2 ? 9 : 18;
            Assert.That(counter.Value, Is.EqualTo(perInvocation * 10001));
            Assert.That(firstAllocated, Is.Zero);
            Assert.That(repeatedAllocated, Is.Zero);
            TestContext.WriteLine(
                $"AdaptiveAction: arity={arity}, registration={registration}, deferred={deferred}, first={firstAllocated}, repeated={repeatedAllocated} bytes.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThrowingRangePreparesCallbacksAlreadyAdded(bool deferred)
        {
            Invoke(CreateInvocation(0, 3, deferred, new Counter()), 2000);
            var actions = new AdaptiveAction(deferred);
            var counter = new Counter();
            var entry = CreateCallback(counter);
            var failure = new ApplicationException("range");

            Assert.That(
                Assert.Throws<ApplicationException>(() => actions.AddRange(ThrowAfter(entry, failure))),
                Is.SameAs(failure));
            var invoke = new Action(actions.Invoke);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Invoke(invoke, 1);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(counter.Value, Is.EqualTo(3));
            Assert.That(allocated, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StreamingRangeCanInvokeAndReplaceAlreadyPreparedEntries(bool deferred)
        {
            Invoke(CreateInvocation(0, 3, deferred, new Counter()), 2000);
            var actions = new AdaptiveAction(deferred);
            var firstCounter = new Counter();
            var secondCounter = new Counter();
            var first = CreateCallback(firstCounter);
            var second = CreateCallback(secondCounter);
            var invoke = new Action(actions.Invoke);
            var allocationDuringRange = new long[1];

            actions.AddRange(YieldInvokeAndReplace(actions, first, second, invoke, allocationDuringRange));
            var before = GC.GetAllocatedBytesForCurrentThread();
            Invoke(invoke, 1);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions.Contains(second), Is.True);
            Assert.That(firstCounter.Value, Is.EqualTo(3));
            Assert.That(secondCounter.Value, Is.EqualTo(3));
            Assert.That(allocationDuringRange[0], Is.Zero);
            Assert.That(allocated, Is.Zero);
        }

        // Registration and lifetime
        [TestCase(false)]
        [TestCase(true)]
        public void SelfAddRangePreservesFiniteLogicalCopy(bool deferred)
        {
            var actions = new AdaptiveAction(deferred);
            var copied = false;
            var firstCalls = 0;
            var secondCalls = 0;
            var lateCalls = 0;
            var late = (Action)(() => lateCalls++);

            actions.Add(() =>
            {
                firstCalls++;

                if (copied) return;

                copied = true;
                actions.Add(late);
                actions.AddRange(actions);
            });

            actions.Add(() => secondCalls++);

            actions.Invoke();
            Assert.That(actions.Count, Is.EqualTo(6));
            Assert.That(firstCalls, Is.EqualTo(deferred ? 1 : 2));
            Assert.That(secondCalls, Is.EqualTo(deferred ? 1 : 2));
            Assert.That(lateCalls, Is.EqualTo(deferred ? 0 : 2));

            actions.Invoke();
            Assert.That(actions.Count, Is.EqualTo(6));
            Assert.That(firstCalls, Is.EqualTo(deferred ? 3 : 4));
            Assert.That(secondCalls, Is.EqualTo(deferred ? 3 : 4));
            Assert.That(lateCalls, Is.EqualTo(deferred ? 2 : 4));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingEarlierDuplicateKeepsCurrentMulticastEntryActive(bool deferred)
        {
            var actions = new AdaptiveAction(deferred);
            var firstCalls = 0;
            var tailCalls = 0;
            var entry = (Action)null;

            entry = () =>
            {
                firstCalls++;

                if (firstCalls == 2) actions.Remove(entry);
            };

            entry += () => tailCalls++;
            actions.Add(entry);
            actions.Add(entry);

            actions.Invoke();
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(firstCalls, Is.EqualTo(2));
            Assert.That(tailCalls, Is.EqualTo(2));

            actions.Invoke();
            Assert.That(firstCalls, Is.EqualTo(3));
            Assert.That(tailCalls, Is.EqualTo(3));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovedCallbacksAndTargetsAreNotRetainedByTheCache(bool clear)
        {
            var actions = new AdaptiveAction();
            var references = RegisterAndRelease(actions, clear);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.That(references.Callback.IsAlive, Is.False);
            Assert.That(references.Target.IsAlive, Is.False);
            GC.KeepAlive(actions);
        }

        // Helpers
        private class Counter
        {
            public int Value { get; set; }
        }

        private static Action CreateInvocation(int arity, int registration, bool deferred, Counter counter)
        {
            switch (arity)
            {
                case 0:
                {
                    var actions = new AdaptiveAction(deferred);
                    var entry = CreateCallback(counter);
                    if (registration == 0) actions.Add(entry);
                    else Register(actions, entry, registration);

                    return actions.Invoke;
                }
                case 1:
                {
                    var actions = new AdaptiveAction<int>(deferred);
                    var entry = (Action<int>)(value => counter.Value += value);
                    entry += value => counter.Value += value * 2;

                    if (registration == 0) actions.Add(entry);
                    else Register(actions, entry, registration);

                    return () => actions.Invoke(1);
                }
                case 2:
                {
                    var actions = new AdaptiveAction<int, int>(deferred);
                    var entry = (Action<int, int>)((first, second) => counter.Value += first + second);
                    entry += (first, second) => counter.Value += (first + second) * 2;

                    if (registration == 0) actions.Add(entry);
                    else Register(actions, entry, registration);

                    return () => actions.Invoke(1, 2);
                }
                default:
                {
                    var actions = new AdaptiveAction<int, int, int>(deferred);
                    var entry = (Action<int, int, int>)((first, second, third) =>
                        counter.Value += first + second + third);
                    entry += (first, second, third) => counter.Value += (first + second + third) * 2;

                    if (registration == 0) actions.Add(entry);
                    else Register(actions, entry, registration);

                    return () => actions.Invoke(1, 2, 3);
                }
            }
        }

        private static Action CreateCallback(Counter counter)
        {
            var entry = (Action)(() => counter.Value++);
            entry += () => counter.Value += 2;
            return entry;
        }

        private static void Register<T>(AdaptiveCollection<T> actions, T entry, int registration)
        {
            switch (registration)
            {
                case 1:
                    actions.Add(entry);
                    break;
                case 2:
                    ((ICollection<T>)actions).Add(entry);
                    break;
                default:
                    actions.AddRange(new[] { entry });
                    break;
            }
        }

        private static IEnumerable<Action> ThrowAfter(Action entry, Exception exception)
        {
            yield return entry;
            throw exception;
        }

        private static IEnumerable<Action> YieldInvokeAndReplace(
            AdaptiveAction actions,
            Action first,
            Action second,
            Action invoke,
            long[] allocation)
        {
            yield return first;
            var before = GC.GetAllocatedBytesForCurrentThread();
            Invoke(invoke, 1);
            allocation[0] = GC.GetAllocatedBytesForCurrentThread() - before;
            actions.Clear();
            yield return second;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Invoke(Action invoke, int repetitions)
        {
            for (var index = 0; index < repetitions; index++) invoke();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Callback, WeakReference Target) RegisterAndRelease(
            AdaptiveAction actions,
            bool clear)
        {
            var counter = new Counter();
            var entry = CreateCallback(counter);
            actions.Add(entry);

            if (clear) actions.Clear();
            else actions.Remove(entry);

            return (new WeakReference(entry), new WeakReference(counter));
        }
    }
}
