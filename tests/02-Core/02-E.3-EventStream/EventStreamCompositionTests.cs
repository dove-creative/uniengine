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
            using var stream = new EventStream<int, Phase>(Phase.Render, Phase.Update, Phase.Update);
            var checks = 0;
            var bits = new Filter<int>("mask", value => { checks++; return (value & 5) != 0; });
            using var render = stream.Where(bits).Branch();
            using var update = stream.ForOrder(Phase.Update).Where(bits).Branch();
            var calls = new List<(Phase, int)>();
            var renders = render.Subscribe(value => calls.Add((Phase.Render, value)));
            update.Subscribe(value => calls.Add((Phase.Update, value)));
            var sharedCalls = 0;
            stream.ForOrder(Phase.Update).Where(bits).Subscribe(_ => sharedCalls++);

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
            Assert.That(stream.Orders, Is.EqualTo(new[] { Phase.Update, Phase.Render }));
            Assert.That(render.IsDisposed, Is.True);
            Assert.That(update.IsDisposed, Is.False);
        }

        // I2: Releasing the last branch subscription removes its filter while the parent can publish again.
        [Test]
        public void FilteredBranchReleasesItsUpstreamPathWithItsLastSubscription()
        {
            using var parent = new EventStream<int>();
            using var branch = parent.Where(new Filter<int>("mask", value => (value & 6) != 0)).Branch();
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
    }
}
