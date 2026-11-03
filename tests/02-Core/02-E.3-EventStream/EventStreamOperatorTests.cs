using System.Collections.Generic;
using Blackthunder.UniEngine.Core;
using Blackthunder.UniEngine.Core.EventStreamCore;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamOperatorTests
    {
        // Content
        // O1: Only the prefix filter stops evaluating after its first false result.
        [Test]
        public void SkipWhileStopsCheckingAtFirstFalseAndWhereKeepsChecking()
        {
            using var source = new EventStream<int>();
            var skipChecks = new List<int>();
            var whereChecks = new List<int>();
            using var skipped = source.SkipWhile(value =>
            {
                skipChecks.Add(value);
                return value % 2 == 1;
            });
            using var filtered = source.Where(value =>
            {
                whereChecks.Add(value);
                return value % 2 == 1;
            });
            var skippedValues = new List<int>();
            var filteredValues = new List<int>();
            using var skipSubscription = skipped.Subscribe(skippedValues.Add);
            using var whereSubscription = filtered.Subscribe(filteredValues.Add);

            skipped.Enabled = false;
            filtered.Enabled = false;
            source.OnNext(0);
            skipped.Enabled = true;
            filtered.Enabled = true;
            for (var value = 1; value <= 4; value++) source.OnNext(value);

            Assert.That(skipChecks, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(skippedValues, Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(whereChecks, Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(filteredValues, Is.EqualTo(new[] { 1, 3 }));
        }

        // O2: A nonpositive count completes only when an enabled input reaches Take.
        [TestCase(0)]
        [TestCase(-1)]
        public void NonpositiveTakeCompletesOnFirstEnabledInputAndReleasesItsUpstream(int count)
        {
            using var source = new EventStream<int>();
            using var filtered = source.Where(static value => value > 0);
            using var taken = filtered.Take(count);
            var sourceValues = new List<int>();
            var takenValues = new List<int>();
            var completions = 0;
            using var original = source.Subscribe(sourceValues.Add);
            using var selected = taken.Subscribe(takenValues.Add, () => completions++, null);

            taken.Enabled = false;
            source.OnNext(1);
            Assert.That(taken.IsDisposed, Is.False);
            Assert.That(completions, Is.Zero);

            taken.Enabled = true;
            source.OnNext(2);
            Assert.That(taken.IsDisposed, Is.True);
            Assert.That(filtered.IsDisposed, Is.True);
            source.OnNext(3);

            Assert.That(completions, Is.EqualTo(1));
            Assert.That(takenValues, Is.Empty);
            Assert.That(sourceValues, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        // O2: Late subscribers share the operator's remaining count.
        [Test]
        public void LateSubscriberSharesTheRemainingTakeWindow()
        {
            using var source = new EventStream<int>();
            using var taken = source.Take(2);
            var earlyValues = new List<int>();
            var lateValues = new List<int>();
            var earlyCompletions = 0;
            var lateCompletions = 0;
            using var early = taken.Subscribe(earlyValues.Add, () => earlyCompletions++, null);
            source.OnNext(1);
            using var late = taken.Subscribe(lateValues.Add, () => lateCompletions++, null);
            source.OnNext(2);
            source.OnNext(3);

            Assert.That(earlyValues, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(lateValues, Is.EqualTo(new[] { 2 }));
            Assert.That(earlyCompletions, Is.EqualTo(1));
            Assert.That(lateCompletions, Is.EqualTo(1));
            Assert.That(taken.IsDisposed, Is.True);
        }

        // O3: A closed external gate ends this branch on the rejected input.
        [Test]
        public void TakeWhileCompletesOnFirstClosedGateAndLeavesOtherSourceSubscribersActive()
        {
            using var source = new EventStream<int>();
            var gateOpen = true;
            var checks = 0;
            using var taken = source.TakeWhile(() =>
            {
                checks++;
                return gateOpen;
            });
            var sourceValues = new List<int>();
            var takenValues = new List<int>();
            var completions = 0;
            using var original = source.Subscribe(sourceValues.Add);
            using var selected = taken.Subscribe(takenValues.Add, () => completions++, null);

            source.OnNext(1);
            gateOpen = false;
            source.OnNext(2);
            Assert.That(taken.IsDisposed, Is.True);
            gateOpen = true;
            source.OnNext(3);

            Assert.That(checks, Is.EqualTo(2));
            Assert.That(takenValues, Is.EqualTo(new[] { 1 }));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(sourceValues, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        // O4: Each appended base stream keeps its own subscription lifetime.
        [Test]
        public void AppendCreatesIndependentChildrenThatStayAvailableAfterUnsubscription()
        {
            using var source = new EventStream<int>();
            using var first = source.Append();
            using var second = source.Append();
            Assert.That(first, Is.TypeOf<EventStream<int>>());
            Assert.That(first, Is.Not.SameAs(source));
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.TerminationMode, Is.EqualTo(TerminationMode.Complete));

            var firstValues = new List<int>();
            var secondValues = new List<int>();
            using var initial = first.Subscribe(firstValues.Add);
            using var sibling = second.Subscribe(secondValues.Add);
            source.OnNext(1);
            initial.Dispose();
            source.OnNext(2);
            using var renewed = first.Subscribe(firstValues.Add);
            source.OnNext(3);

            Assert.That(firstValues, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(secondValues, Is.EqualTo(new[] { 1, 2, 3 }));
        }
    }
}
