using System;
using System.Collections.Generic;
using System.IO;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniEngine.Core;
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

            var skipped = source.SkipWhile(value =>
            {
                skipChecks.Add(value);
                return value % 2 == 1;
            });

            var filtered = source.Where(value =>
            {
                whereChecks.Add(value);
                return value % 2 == 1;
            });

            var skippedValues = new List<int>();
            var filteredValues = new List<int>();
            using var skipSubscription = skipped.Subscribe(skippedValues.Add);
            using var whereSubscription = filtered.Subscribe(filteredValues.Add);

            source.Enabled = false;
            source.OnNext(0);
            source.Enabled = true;

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
            var filtered = source.Where(static value => value > 0);
            var taken = filtered.Take(count);
            var sourceValues = new List<int>();
            var takenValues = new List<int>();
            var completions = 0;
            using var original = source.Subscribe(sourceValues.Add);
            using var selected = taken.Subscribe(takenValues.Add, () => completions++, null);

            source.Enabled = false;
            source.OnNext(1);
            Assert.That(((Handle)selected).IsDisposed, Is.False);
            Assert.That(completions, Is.Zero);

            source.Enabled = true;
            source.OnNext(2);
            Assert.That(((Handle)selected).IsDisposed, Is.True);
            Assert.That(source.NodeCount, Is.Zero);
            source.OnNext(3);

            Assert.That(completions, Is.EqualTo(1));
            Assert.That(takenValues, Is.Empty);
            Assert.That(sourceValues, Is.EqualTo(new[] { 2, 3 }));
        }

        // O2: Subscribers of one Take share its remaining count.
        [Test]
        public void LateSubscriberContinuesTheRemainingTakeCount()
        {
            using var source = new EventStream<int>();
            var taken = source.Take(2);
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
            Assert.That(source.NodeCount, Is.Zero);
        }

        // O2: Upstream values reach Take only after its first subscription connects it.
        [Test]
        public void TakeCountsUpstreamValuesFromItsFirstSubscription()
        {
            using var source = new EventStream<int>();
            using var taken = source.Take(1);
            var values = new List<int>();

            source.OnNext(1);
            Assert.That(taken.IsDisposed, Is.False);
            using var subscription = taken.Subscribe(values.Add);
            source.OnNext(2);

            Assert.That(values, Is.EqualTo(new[] { 2 }));
            Assert.That(taken.IsDisposed, Is.True);
        }

        // O2: Subscribing to a finished Take is rejected; calling Take again starts a new count.
        [Test]
        public void FinishedTakeRejectsSubscriptionsAndANewTakeStartsAgain()
        {
            using var source = new EventStream<int>();
            using var first = source.Take(1);
            var values = new List<int>();
            using var subscription = first.Subscribe(values.Add);

            source.OnNext(1);
            Assert.Throws<ObjectDisposedException>(() => first.Subscribe(values.Add));
            using var second = source.Take(1);
            using var renewed = second.Subscribe(values.Add);
            source.OnNext(2);

            Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
        }

        // O3: A closed external gate ends this branch on the rejected input.
        [Test]
        public void TakeWhileCompletesOnFirstClosedGateAndLeavesOtherSourceSubscribersActive()
        {
            using var source = new EventStream<int>();
            var gateOpen = true;
            var checks = 0;

            var taken = source.TakeWhile(_ =>
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
            Assert.That(((Handle)selected).IsDisposed, Is.True);
            Assert.That(source.NodeCount, Is.Zero);
            gateOpen = true;
            source.OnNext(3);

            Assert.That(checks, Is.EqualTo(2));
            Assert.That(takenValues, Is.EqualTo(new[] { 1 }));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(sourceValues, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void DebugWritesOnlyPassingValuesAndForwardsThemUnchanged()
        {
            using var source = new EventStream<int>();
            var values = new List<int>();
            source.Where(value => value % 2 == 0).Debug("observed", LogLevel.Warning)
                .Subscribe(values.Add);

            using var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                source.OnNext(1);
                source.OnNext(2);
            }
            finally
            {
                Console.SetOut(original);
            }

            Assert.That(values, Is.EqualTo(new[] { 2 }));
            Assert.That(output.ToString().TrimEnd(), Is.EqualTo("2: observed"));
        }

        [Test]
        public void LateSkipSubscriptionContinuesAfterTheConsumedPrefix()
        {
            using var source = new EventStream<int>();
            var skipped = source.Skip(1);
            var early = new List<int>();
            var late = new List<int>();
            skipped.Subscribe(early.Add);
            source.OnNext(1);
            skipped.Subscribe(late.Add);
            source.OnNext(2);
            source.OnNext(3);
            Assert.That(early, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(late, Is.EqualTo(new[] { 2, 3 }));
        }

        // Stateful operators keep the graph free of state, so their predicates are never shared.
        [Test]
        public void StatefulOperatorsStartSeparateRelationsForTheSamePredicate()
        {
            using var source = new EventStream<int>();
            var checks = 0;
            Func<int, bool> filter = _ => { checks++; return true; };
            using var first = source.TakeWhile(filter);
            using var second = source.TakeWhile(filter);
            using var firstListener = first.Subscribe(_ => { });
            using var secondListener = second.Subscribe(_ => { });

            source.OnNext(1);

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(checks, Is.EqualTo(2));
        }

        // O4: Each branch ends when its last subscription leaves.
        [Test]
        public void BranchesHaveIndependentSubscriptionLifetimes()
        {
            using var source = new EventStream<int>();
            using var first = source.Branch();
            using var second = source.Branch();
            Assert.That(first, Is.TypeOf<EventStream<int>>());
            Assert.That(first, Is.Not.SameAs(source));
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(source.NodeCount, Is.Zero);

            var firstValues = new List<int>();
            var secondValues = new List<int>();
            using var initial = first.Subscribe(firstValues.Add);
            using var sibling = second.Subscribe(secondValues.Add);
            source.OnNext(1);
            initial.Dispose();
            source.OnNext(2);
            Assert.That(first.IsDisposed, Is.True);
            source.OnNext(3);

            Assert.That(firstValues, Is.EqualTo(new[] { 1 }));
            Assert.That(secondValues, Is.EqualTo(new[] { 1, 2, 3 }));
        }
    }
}
