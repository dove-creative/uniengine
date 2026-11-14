using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamCompositionTests
    {
        private enum Phase { Update, Render }

        // I1: The same filter is shared within each group and evaluated separately between groups.
        [Test]
        public void GroupedFilterBranchesDeliverInGroupOrderAndAllowIndependentPublication()
        {
            using var stream = new EventStream<int>();
            var checks = 0;
            Func<int, bool> selected = value => { checks++; return value != 2; };
            using var render = stream.ForOrder(Phase.Render).Where(selected).Branch();
            using var update = stream.ForOrder(Phase.Update).Where(selected).Branch();
            var calls = new List<(Phase, int)>();
            var renders = render.Subscribe(value => calls.Add((Phase.Render, value)));
            update.Subscribe(value => calls.Add((Phase.Update, value)));
            var sharedCalls = 0;
            stream.ForOrder(Phase.Update).Where(selected).Subscribe(_ => sharedCalls++);

            stream.OnNext(2);
            stream.OnNext(1);
            stream.OnNext(6);
            renders.Dispose();
            stream.OnNext(4);
            update.OnNext(9);

            Assert.That(calls, Is.EqualTo(new[]
            {
                (Phase.Update, 1), (Phase.Render, 1),
                (Phase.Update, 6), (Phase.Render, 6),
                (Phase.Update, 4), (Phase.Update, 9),
            }));

            Assert.That(checks, Is.EqualTo(7));
            Assert.That(sharedCalls, Is.EqualTo(3));
            Assert.That(render.IsDisposed, Is.True);
            Assert.That(update.IsDisposed, Is.False);
        }

        // I2: Releasing the last branch subscription removes its filter while the parent can publish again.
        [Test]
        public void FilteredBranchReleasesItsUpstreamPathWithItsLastSubscription()
        {
            using var parent = new EventStream<int>();
            using var branch = parent.Where(value => value > 1).Branch();
            var calls = new List<int>();
            var subscription = branch.Subscribe(calls.Add);

            parent.OnNext(1);
            parent.OnNext(6);
            subscription.Dispose();
            parent.OnNext(4);

            Assert.That(calls, Is.EqualTo(new[] { 6 }));
            Assert.That(branch.IsDisposed, Is.True);
            Assert.That(parent.NodeCount, Is.Zero);
            Assert.That(parent.IsDisposed, Is.False);
        }

        // I3: Plain subscribers run first because their gate stays the first child of the node.
        [Test]
        public void UnorderedSubscribersAndOrderedGroupsReceiveTheValue()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            using var render = stream.ForOrder(Phase.Render).Subscribe(_ => calls.Add("render"));
            using var update = stream.ForOrder(Phase.Update).Subscribe(_ => calls.Add("update"));
            using var plain = stream.Subscribe(_ => calls.Add("plain"));

            stream.OnNext(1);

            Assert.That(calls, Is.EquivalentTo(new[] { "plain", "update", "render" }));
        }

        // I4: Groups order the children of the position they are used at, not only the root.
        [Test]
        public void GroupsCanOrderPartOfAPath()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            Func<int, bool> all = _ => true;
            using var render = stream.Where(all).ForOrder(Phase.Render).Subscribe(_ => calls.Add("render"));
            using var update = stream.Where(all).ForOrder(Phase.Update).Subscribe(_ => calls.Add("update"));

            stream.OnNext(1);

            Assert.That(calls, Is.EqualTo(new[] { "update", "render" }));
            // Rows do not count as nodes: only WhereNode and OrderedNode remain.
            Assert.That(stream.NodeCount, Is.EqualTo(2));
        }
    }
}
