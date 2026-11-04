using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamGraphTests
    {
        // Content --------------------
        // Sharing and traversal
        [Test]
        public void OnlyTheSameFilterReferenceUnderTheSameParentIsShared()
        {
            using var stream = new EventStream<int>();
            Func<int, bool> predicate = value => value > 0;
            var first = new Filter<int>("positive", predicate);
            var equivalent = new Filter<int>("positive", predicate);
            var path = stream.Where(first);
            Assert.That(stream.NodeCount, Is.Zero);

            var direct = path.Subscribe(_ => { });
            var duplicate = stream.Where(first).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            var separate = stream.Where(equivalent).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(2));
            var nested = path.Where(equivalent).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(3));

            direct.Dispose();
            duplicate.Dispose();
            Assert.That(stream.NodeCount, Is.EqualTo(3));
            nested.Dispose();
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            separate.Dispose();
            Assert.That(stream.NodeCount, Is.Zero);
            Assert.That(stream.IsDisposed, Is.False);
        }

        [Test]
        public void SharedPrefixRunsOnceAndRejectingItSkipsEveryDescendant()
        {
            using var stream = new EventStream<int>();
            var prefixChecks = 0;
            var leftChecks = 0;
            var rightChecks = 0;
            var deliveries = 0;
            var prefix = new Filter<int>("positive", value => { prefixChecks++; return value > 0; });
            var left = new Filter<int>("left", _ => { leftChecks++; return true; });
            var right = new Filter<int>("right", _ => { rightChecks++; return true; });
            stream.Where(prefix).Where(left).Subscribe(_ => deliveries++);
            stream.Where(prefix).Where(left).Subscribe(_ => deliveries++);
            stream.Where(prefix).Where(right).Subscribe(_ => deliveries++);
            Assert.That(stream.NodeCount, Is.EqualTo(3));

            stream.OnNext(0);
            Assert.That((prefixChecks, leftChecks, rightChecks, deliveries), Is.EqualTo((1, 0, 0, 0)));
            stream.OnNext(1);
            Assert.That((prefixChecks, leftChecks, rightChecks, deliveries), Is.EqualTo((2, 1, 1, 3)));
        }

        [Test]
        public void PublicationVisitsSubscribersBeforeChildrenInCreationOrder()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var left = stream.Where(new Filter<int>("left", _ => true));
            var right = stream.Where(new Filter<int>("right", _ => true));
            var leaf = left.Where(new Filter<int>("leaf", _ => true));
            leaf.Subscribe(_ => calls.Add("leaf"));
            right.Subscribe(_ => calls.Add("right"));
            left.Subscribe(_ => calls.Add("left1"));
            stream.Subscribe(_ => calls.Add("root1"));
            left.Subscribe(_ => calls.Add("left2"));
            stream.Subscribe(_ => calls.Add("root2"));

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "root1", "root2", "left1", "left2", "leaf", "right" }));
        }

        [Test]
        public void MaximumPathDepthIncludesTheBranchConnection()
        {
            using var stream = new EventStream<int>();
            var checks = 0;
            var filter = new Filter<int>("step", _ => { checks++; return true; });
            var path = stream.Where(filter);
            for (var depth = 1; depth < 255; depth++) path = path.Where(filter);
            using var branch = path.Branch();
            var values = new List<int>();
            branch.Subscribe(values.Add);

            Assert.Throws<InvalidOperationException>(() => branch.Where(filter));
            Assert.Throws<InvalidOperationException>(() => path.Where(filter).Branch());
            stream.OnNext(7);
            Assert.That(checks, Is.EqualTo(255));
            Assert.That(values, Is.EqualTo(new[] { 7 }));
            Assert.That(stream.NodeCount, Is.EqualTo(256));
        }

        [Test]
        public void ManySiblingFiltersKeepCreationOrderAfterRemovalAndReregistration()
        {
            using var stream = new EventStream<int>();
            var visited = new List<int>();
            var filters = new Filter<int>[32];
            var handles = new IDisposable[filters.Length];
            for (var index = 0; index < filters.Length; index++)
            {
                var id = index;
                filters[index] = new Filter<int>("sibling", _ => { visited.Add(id); return true; });
                handles[index] = stream.Where(filters[index]).Subscribe(_ => { });
            }

            handles[4].Dispose();
            stream.Where(filters[4]).Subscribe(_ => { });
            stream.Where(filters[7]).Subscribe(_ => { });
            stream.OnNext(1);
            var expected = new List<int>();
            for (var index = 0; index < filters.Length; index++)
                if (index != 4) expected.Add(index);
            expected.Add(4);
            Assert.That(visited, Is.EqualTo(expected));
            Assert.That(stream.NodeCount, Is.EqualTo(filters.Length));
        }

        // Take
        [Test]
        public void TakeCountsOnlyArrivalsThenCompletesItsEntireSubtree()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var checkedValues = 0;
            var gated = stream.Where(new Filter<int>("positive", value => value > 0));
            var take = gated.Take(2);
            var onlyEven = new Filter<int>("even", value => { checkedValues++; return value % 2 == 0; });
            var handle = take.Where(onlyEven).Subscribe(
                value => calls.Add($"value:{value}"), () => calls.Add("done"), _ => calls.Add("error"));
            stream.OnNext(-1);
            stream.OnNext(2);
            stream.OnNext(4);
            stream.OnNext(6);
            Assert.That(checkedValues, Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(new[] { "value:2", "value:4", "done" }));
            Assert.That(((Blackthunder.UniEngine.Core.Handle)handle).IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.Zero);
            Assert.That(stream.IsDisposed, Is.False);
        }

        [TestCase(0)]
        [TestCase(int.MinValue)]
        public void NonpositiveTakeCompletesOnTheFirstArrivalWithoutEvaluatingDescendants(int count)
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var path = stream.Where(new Filter<int>("positive", value => value > 0)).Take(count);
            path.Where(new Filter<int>("child", _ => { calls.Add("check"); return true; }))
                .Subscribe(_ => calls.Add("value"), () => calls.Add("done"), _ => calls.Add("error"));
            stream.OnNext(0);
            Assert.That(calls, Is.Empty);
            stream.OnNext(1);
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(new[] { "done" }));
            Assert.That(stream.NodeCount, Is.Zero);
        }

        [Test]
        public void FinalTakeCompletesALateBranchSubscriptionWithoutDeliveringTheCurrentValue()
        {
            using var parent = new EventStream<int>();
            using var branch = parent.Take(1).Branch();
            var values = new List<int>();
            var completions = 0;
            var pending = (Blackthunder.UniEngine.Core.Handle)null;
            branch.Subscribe(_ => pending = (Blackthunder.UniEngine.Core.Handle)branch.Subscribe(
                values.Add, () => completions++, null));

            parent.OnNext(1);
            Assert.That(values, Is.Empty);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(pending.IsDisposed && branch.IsDisposed, Is.True);
            Assert.That(parent.NodeCount, Is.Zero);
        }

        [Test]
        public void RelationConnectsThroughOneNodeInItsUpstreamGraph()
        {
            using var stream = new EventStream<int>();
            var take = stream.Take(2);
            Assert.That(stream.NodeCount, Is.Zero);

            take.Subscribe(_ => { });
            take.Where(new Filter<int>("all", _ => true)).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            Assert.That(take.NodeCount, Is.EqualTo(1));

            stream.OnNext(1);
            stream.OnNext(2);
            Assert.That(stream.NodeCount, Is.Zero);
        }
    }
}
