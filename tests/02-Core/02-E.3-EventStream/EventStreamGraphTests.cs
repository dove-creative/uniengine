using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamGraphTests
    {
        // Content --------------------
        // Predicates
        private static bool IsPositive(int value) => value > 0;
        private static bool IsNegative(int value) => value < 0;
        private static Func<int, bool> Above(int limit) => value => value > limit;

        // Sharing and traversal
        [Test]
        public void OnlyEqualDelegatesUnderTheSameParentAreShared()
        {
            using var stream = new EventStream<int>();
            Func<int, bool> first = IsPositive;
            Func<int, bool> equal = IsPositive;
            Func<int, bool> sameBody = value => value > 0;
            Assert.That(equal, Is.Not.SameAs(first));
            var path = stream.Where(first);
            Assert.That(stream.NodeCount, Is.Zero);

            var direct = path.Subscribe(_ => { });
            var duplicate = stream.Where(equal).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            var separate = stream.Where(sameBody).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(2));
            var nested = path.Where(sameBody).Subscribe(_ => { });
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
        public void LambdasWithDifferentClosuresAreNotShared()
        {
            using var stream = new EventStream<int>();

            stream.Where(Above(1)).Subscribe(_ => { });
            stream.Where(Above(1)).Subscribe(_ => { });

            Assert.That(stream.NodeCount, Is.EqualTo(2));
        }

        // Many siblings must still share only equal delegates.
        [Test]
        public void ManySiblingsShareEqualDelegatesOnly()
        {
            using var stream = new EventStream<int>();
            for (var limit = 0; limit < 8; limit++) stream.Where(Above(limit)).Subscribe(_ => { });

            stream.Where(IsPositive).Subscribe(_ => { });
            var nodes = stream.NodeCount;

            stream.Where(IsPositive).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(nodes));
            stream.Where(IsNegative).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(nodes + 1));
            stream.Where(Above(0)).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(nodes + 2));
        }

        // The shared gate is created after these children and must still run before them.
        [Test]
        public void SubscribersRunBeforeChildrenThatWereCreatedEarlier()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            using var branch = stream.Branch();
            using var relation = branch.Subscribe(_ => calls.Add("relation"));
            using var child = stream.Where(_ => true).Subscribe(_ => calls.Add("child"));

            var first = stream.Subscribe(_ => calls.Add("first"));
            first.Dispose();
            using var second = stream.Subscribe(_ => calls.Add("second"));
            stream.OnNext(1);

            Assert.That(calls, Is.EqualTo(new[] { "second", "relation", "child" }));
        }

        [Test]
        public void SharedPrefixRunsOnceAndRejectingItSkipsEveryDescendant()
        {
            using var stream = new EventStream<int>();
            var prefixChecks = 0;
            var leftChecks = 0;
            var rightChecks = 0;
            var deliveries = 0;
            Func<int, bool> prefix = value => { prefixChecks++; return value > 0; };
            Func<int, bool> left = _ => { leftChecks++; return true; };
            Func<int, bool> right = _ => { rightChecks++; return true; };
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
            var left = stream.Where(_ => true);
            var right = stream.Where(_ => true);
            var leaf = left.Where(_ => true);
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
            Func<int, bool> filter = _ => { checks++; return true; };
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
            var filters = new Func<int, bool>[32];
            var handles = new IDisposable[filters.Length];
            for (var index = 0; index < filters.Length; index++)
            {
                var id = index;
                filters[index] = _ => { visited.Add(id); return true; };
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
            var gated = stream.Where(value => value > 0);
            var take = gated.Take(2);
            Func<int, bool> onlyEven = value => { checkedValues++; return value % 2 == 0; };
            var handle = take.Where(onlyEven).Subscribe(
                value => calls.Add($"value:{value}"),
                () => calls.Add("done"),
                _ => calls.Add("error"));
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
            var path = stream.Where(value => value > 0).Take(count);
            path.Where(_ => { calls.Add("check"); return true; })
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
            branch.Subscribe(
                _ => pending = (Blackthunder.UniEngine.Core.Handle)branch.Subscribe(
                    values.Add,
                    () => completions++,
                    null));

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
            take.Where(_ => true).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            Assert.That(take.NodeCount, Is.EqualTo(1));

            stream.OnNext(1);
            stream.OnNext(2);
            Assert.That(stream.NodeCount, Is.Zero);
        }
    }
}
