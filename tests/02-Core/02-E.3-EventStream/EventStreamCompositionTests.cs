using System.Collections.Generic;
using Blackthunder.UniEngine.Core;
using Blackthunder.UniEngine.Core.EventStreamCore;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamCompositionTests
    {
        private enum Phase { Update, Render }

        // I1: Route matching inputs into ordered groups and release only the connecting subscription.
        [Test]
        public void RoutedConnectionDeliversMatchingValuesInGroupOrderUntilReleased()
        {
            using var router = EventRouterExtensions.Route<int>(static value => (uint)value);
            using var ordered = new OrderedEventStream<int, Phase>(Phase.Render, new[] { Phase.Update, Phase.Update });
            var calls = new List<(Phase, int)>();
            using var render = ordered.Subscribe(value => calls.Add((Phase.Render, value)));
            using var update = ordered.Subscribe(Phase.Update, value => calls.Add((Phase.Update, value)));
            router.Chain(new[] { 1, 4 }, ordered, out var handle);
            using var connection = handle;

            router.OnNext(2);
            router.OnNext(1);
            router.OnNext(6);
            connection.Dispose();
            router.OnNext(4);
            ordered.OnNext(9);

            Assert.That(calls, Is.EqualTo(new[]
            {
                (Phase.Update, 1), (Phase.Render, 1),
                (Phase.Update, 6), (Phase.Render, 6),
                (Phase.Update, 9), (Phase.Render, 9),
            }));
        }

        // I2: A router chained by Route filters source values and releases the source connection with its last subscription.
        [Test]
        public void ChainedRouterFiltersSourceValuesUntilItsLastSubscriptionIsReleased()
        {
            using var source = new EventStream<int>().With(TerminationMode.NoSubscribers);
            using var router = source.Route(static value => (uint)value);
            var calls = new List<int>();
            var subscription = router.Subscribe(new[] { 2, 4 }, calls.Add);

            source.OnNext(1);
            source.OnNext(6);
            subscription.Dispose();

            Assert.That(calls, Is.EqualTo(new[] { 6 }));
            Assert.That(router.IsDisposed, Is.True);
            Assert.That(source.IsDisposed, Is.True);
        }
    }
}
