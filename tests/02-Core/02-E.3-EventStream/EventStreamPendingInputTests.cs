using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamPendingInputTests
    {
        // Content
        // O6: Direct values advance counters without connecting; disabled values do not.
        [TestCase(false)]
        [TestCase(true)]
        public void DirectInputAdvancesCountersBeforeSubscription(bool skip)
        {
            using var source = new EventStream<int>();
            using var relation = skip ? source.Skip(2) : source.Take(2);
            var values = new List<int>();
            var completions = 0;
            source.OnNext(99);
            relation.Enabled = false;
            relation.OnNext(99);
            relation.Enabled = true;
            relation.OnNext(1);
            Assert.That(source.NodeCount, Is.Zero);
            Assert.That(relation.IsDisposed, Is.False);

            using var subscription = relation.Subscribe(values.Add, () => completions++, null);
            source.OnNext(2);
            source.OnNext(3);

            Assert.That(values, Is.EqualTo(skip ? new[] { 3 } : new[] { 2 }));
            Assert.That(completions, Is.EqualTo(skip ? 0 : 1));
            Assert.That(relation.IsDisposed, Is.EqualTo(!skip));
            Assert.That(source.NodeCount, Is.EqualTo(skip ? 1 : 0));
        }

        // O6: Direct predicates can open SkipWhile or finish TakeWhile before anyone subscribes.
        [TestCase(false)]
        [TestCase(true)]
        public void DirectInputEvaluatesConditionsBeforeSubscription(bool skip)
        {
            using var source = new EventStream<int>();
            var checks = new List<int>();
            Func<int, bool> positive = value =>
            {
                checks.Add(value);
                return value > 0;
            };
            using var relation = skip ? source.SkipWhile(positive) : source.TakeWhile(positive);
            source.OnNext(-1);
            relation.Enabled = false;
            relation.OnNext(-1);
            Assert.That(checks, Is.Empty);
            relation.Enabled = true;
            relation.OnNext(1);
            relation.OnNext(-1);
            Assert.That(checks, Is.EqualTo(new[] { 1, -1 }));
            Assert.That(source.NodeCount, Is.Zero);

            if (skip)
            {
                var values = new List<int>();
                using var subscription = relation.Subscribe(values.Add);
                source.OnNext(1);
                Assert.That(values, Is.EqualTo(new[] { 1 }));
                Assert.That(checks, Is.EqualTo(new[] { 1, -1 }), "An open SkipWhile stops evaluating.");
            }
            else
            {
                Assert.That(relation.IsDisposed, Is.True);
                Assert.Throws<ObjectDisposedException>(() => relation.Subscribe(_ => { }));
            }
        }

        // O6: Completion before the first subscription does not create an upstream connection.
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        public void TakeCanFinishBeforeItsFirstSubscription(int count)
        {
            using var source = new EventStream<int>();
            using var taken = source.Take(count);
            taken.Enabled = false;
            taken.OnNext(1);
            Assert.That(taken.IsDisposed, Is.False);
            taken.Enabled = true;
            taken.OnNext(2);

            Assert.That(taken.IsDisposed, Is.True);
            Assert.That(source.NodeCount, Is.Zero);
            Assert.That(source.IsDisposed, Is.False);
            Assert.Throws<ObjectDisposedException>(() => taken.Subscribe(_ => { }));
        }
    }
}
