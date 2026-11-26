using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem;
using BlackThunder.GeoSystem.RouteSystem.Planar;
using SegmentView = BlackThunder.GeoSystem.RouteSystem.Planar.SegmentView;
using BlackThunder.UniEngine.Objects;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;
using PlanarRoute = BlackThunder.GeoSystem.RouteSystem.Planar.Route;
using Route = BlackThunder.GeoSystem.RouteSystem.Route;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("RouteEditing")]
    public class RouteTests
    {
        [Test]
        public void DefaultProvidersAndExplicitOverrideChooseExpectedEqualCostPaths()
        {
            using var a = new Planar.Node(Vector2.Zero);
            using var b = new Planar.Node(Vector2.UnitY);
            using var c = new Planar.Node(Vector2.UnitX);
            using var d = new Planar.Node(new Vector2(2f, 0f));
            using var ab = new WeightedSegment(a, b, 1f);
            using var bd = new WeightedSegment(b, d, 3f);
            using var ac = new WeightedSegment(a, c, 2f);
            using var cd = new WeightedSegment(c, d, 2f);
            using var common = new Route();
            using var planar = new PlanarRoute();
            using var explicitDijkstra = new PlanarRoute(new PathfinderAdapter<Vector2>(new DijkstraPathfinder()));
            foreach (var route in new Route[] { common, planar, explicitDijkstra })
            {
                route.ConnectNode(ConnectionSide.From, a);
                route.ConnectNode(ConnectionSide.To, d);
                Assert.That(route.Length, Is.EqualTo(4f));
            }
            Assert.That(common.Views.Select(view => view.Source), Is.EqualTo(new[] { ab, bd }));
            Assert.That(planar.Views.Select(view => view.Source), Is.EqualTo(new[] { ac, cd }));
            Assert.That(explicitDijkstra.Views.Select(view => view.Source), Is.EqualTo(new[] { ab, bd }));
            Assert.Throws<ArgumentNullException>(() => new Route(null));
            Assert.Throws<ArgumentNullException>(() => new PlanarRoute(null));
        }

        [TestCase(false, ConnectionSide.From)]
        [TestCase(false, ConnectionSide.To)]
        [TestCase(true, ConnectionSide.From)]
        [TestCase(true, ConnectionSide.To)]
        public void FirstEndpointIsRetainedAndSecondEndpointBuildsOrientedViews(bool planar, ConnectionSide firstSide)
        {
            using var graph = new Graph();
            var finder = new Finder();
            using Route route = planar ? new PlanarRoute(new PathfinderAdapter<Vector2>(finder)) : new Route(finder);
            IRoute contract = route;
            var first = firstSide == ConnectionSide.From ? graph.Nodes[0] : graph.Nodes[2];
            Assert.That(contract.TryConnectNode(firstSide, first), Is.True);
            Assert.That(finder.Calls, Is.Zero);
            Assert.That(route.Nodes, Is.EqualTo(new[] { first }));
            Assert.That(route.IsIsolated, Is.False);
            Assert.That(route.Views, Is.Empty);
            Assert.That(route.Length, Is.Zero);
            Assert.That(route.GetCounterNode(first), Is.Null);
            var secondSide = firstSide == ConnectionSide.From ? ConnectionSide.To : ConnectionSide.From;
            route.ConnectNode(secondSide, firstSide == ConnectionSide.From ? graph.Nodes[2] : graph.Nodes[0]);
            Assert.That(finder.Calls, Is.EqualTo(1));
            Assert.That(finder.LastFrom, Is.SameAs(graph.Nodes[0]));
            Assert.That(finder.LastTo, Is.SameAs(graph.Nodes[2]));
            Assert.That(route.From, Is.SameAs(graph.Nodes[0]));
            Assert.That(route.To, Is.SameAs(graph.Nodes[2]));
            Assert.That(route.Views.Select(view => view.IsForward), Is.EqualTo(new[] { true, false }));
            Assert.That(route.Nodes, Is.EquivalentTo(graph.Nodes.Take(3)));
            Assert.That(((INavSegment)route).From, Is.SameAs(route.From));
            Assert.That(((IReadOnlyNavSegment)route).To, Is.SameAs(route.To));
            Assert.That(graph.Nodes[0].Segments.Any(segment => ReferenceEquals(segment, route)), Is.False);
            Assert.Throws<NotSupportedException>(() => ((IList<ISegmentView>)route.Views).Clear());
            if (route is PlanarRoute positioned)
            {
                Assert.That(((IReadOnlySegment<Vector2>)positioned).From, Is.SameAs(route.From));
                PlanarGeoSystemTests.AssertPose(positioned, new Location(1.5f), new Vector2(1.5f, 0f), 0f);
            }
        }

        [Test]
        public void ReplacementRebuildsTheWholePathAndPublishesCompletedStateOnce()
        {
            using var graph = new Graph();
            var finder = new Finder();
            using var route = new PlanarRoute(new PathfinderAdapter<Vector2>(finder));
            var events = new List<int>();
            route.EventHub.Connect(events).OnUpdated(flags =>
            {
                events.Add(flags);
                Assert.That(route.Length, Is.EqualTo(route.Views.Sum(view => view.Source.Length)));
                foreach (var view in route.Views) Assert.That(HasSubscription(view.Source, route), Is.True);
            });
            route.ConnectNode(ConnectionSide.From, graph.Nodes[0]);
            route.ConnectNode(ConnectionSide.To, graph.Nodes[2]);
            route.ConnectNode(ConnectionSide.To, graph.Nodes[3]);
            route.ConnectNode(ConnectionSide.From, graph.Nodes[1]);
            Assert.That(finder.LastFrom, Is.SameAs(graph.Nodes[1]));
            Assert.That(finder.LastTo, Is.SameAs(graph.Nodes[3]));
            Assert.That(route.Views.Select(view => view.Source), Is.EqualTo(graph.Edges.Skip(1).Take(2)));
            Assert.That(HasSubscription(graph.Edges[0], route), Is.False);
            var connect = (int)(GeoEvents.Connect | GeoEvents.Shape);
            var replace = (int)(GeoEvents.AnyConnection | GeoEvents.Shape);
            Assert.That(events, Is.EqualTo(new[] { connect, connect, replace, replace }));
            route.ConnectNode(ConnectionSide.To, graph.Nodes[3]);
            Assert.That(finder.Calls, Is.EqualTo(3));
            Assert.That(events.Count, Is.EqualTo(4));
            graph.Nodes[0].Position = new Vector2(-5f, 0f);
            Assert.That(events.Count, Is.EqualTo(4));
        }

        [TestCase(ConnectionSide.From)]
        [TestCase(ConnectionSide.To)]
        public void DisconnectKeepsTheOtherEndpointAndReconnects(ConnectionSide side)
        {
            using var graph = new Graph();
            using var route = new PlanarRoute();
            route.ConnectNode(ConnectionSide.From, graph.Nodes[0]);
            route.ConnectNode(ConnectionSide.To, graph.Nodes[2]);
            var kept = side == ConnectionSide.From ? graph.Nodes[2] : graph.Nodes[0];
            var removed = side == ConnectionSide.From ? graph.Nodes[0] : graph.Nodes[2];
            var events = new List<int>();
            route.EventHub.Connect(events).OnUpdated(events.Add);
            route.DisconnectNode(side);
            Assert.That(route.Views, Is.Empty);
            Assert.That(route.Nodes.Single(), Is.SameAs(kept));
            Assert.That(route.Length, Is.Zero);
            Assert.That(route.IsIsolated, Is.False);
            Assert.That(route.TryGetPosition(default, out _), Is.False);
            Assert.That(events, Is.EqualTo(new[] { (int)(GeoEvents.Disconnect | GeoEvents.Shape) }));
            route.DisconnectNode(side);
            route.DisconnectNode((INavNode)null);
            route.DisconnectNode(graph.Nodes[4]);
            Assert.That(events.Count, Is.EqualTo(1));
            foreach (var edge in graph.Edges) Assert.That(HasSubscription(edge, route), Is.False);
            route.ConnectNode(side, removed);
            Assert.That(route.Views.Count, Is.EqualTo(2));
            route.DisconnectNode(removed);
            Assert.That(route.Nodes.Single(), Is.SameAs(kept));
            kept.Dispose();
            Assert.That(route.IsDisposed, Is.True);
            Assert.That(removed.IsDisposed, Is.False);
        }

        [Test]
        public void CoincidentEndpointsClearAndLifetimePreserveMetadata()
        {
            using var node = new Planar.Node();
            var finder = new Finder { Handler = (_, _) => throw new Exception("No search is needed.") };
            using var route = new PlanarRoute(new PathfinderAdapter<Vector2>(finder));
            route.ConnectNode(ConnectionSide.From, node);
            route.ConnectNode(ConnectionSide.To, node);
            Assert.That(route.From, Is.SameAs(node));
            Assert.That(route.To, Is.SameAs(node));
            Assert.That(route.Nodes, Is.EqualTo(new[] { node }));
            Assert.That(route.Views, Is.Empty);
            Assert.That(route.TryGetTransform(default, out _), Is.False);
            route.DisconnectNode(node);
            Assert.That(route.IsIsolated, Is.True);
            route.ConnectNode(ConnectionSide.To, node);
            route.ClearNodes();
            node.Dispose();
            Assert.That(route.IsDisposed, Is.False);
            var payload = new Payload();
            var bag = route.Property;
            bag.Add("payload", payload);
            route.Dispose();
            route.Clear();
            Assert.That(route.Property, Is.SameAs(bag));
            Assert.That(bag.Get("payload"), Is.SameAs(payload));
            Assert.That(payload.IsDisposed, Is.False);
            bag.Clear();
        }

        [Test]
        public void FailedSearchAndProviderExceptionPreserveAllOldState()
        {
            using var graph = new Graph();
            var finder = new Finder();
            using var route = new PlanarRoute(new PathfinderAdapter<Vector2>(finder));
            route.ConnectNode(ConnectionSide.From, graph.Nodes[0]);
            route.ConnectNode(ConnectionSide.To, graph.Nodes[2]);
            var views = route.Views.ToArray();
            var updates = 0;
            route.EventHub.Connect(new object()).OnUpdated(_ => updates++);
            Assert.That(route.TryConnectNode(ConnectionSide.To, graph.Nodes[5]), Is.False);
            Assert.Throws<InvalidOperationException>(() => route.ConnectNode(ConnectionSide.To, graph.Nodes[5]));
            finder.Handler = (_, _) => throw new ApplicationException("Provider failed.");
            Assert.Throws<ApplicationException>(() => route.TryConnectNode(ConnectionSide.From, graph.Nodes[3]));
            Assert.That(route.From, Is.SameAs(graph.Nodes[0]));
            Assert.That(route.To, Is.SameAs(graph.Nodes[2]));
            Assert.That(route.Views, Is.EqualTo(views));
            Assert.That(route.Length, Is.EqualTo(2f));
            Assert.That(updates, Is.Zero);
            foreach (var view in views) Assert.That(HasSubscription(view.Source, route), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void InvalidOrStaleProviderResultsAreRejectedBeforeAnyCommit(int scenario)
        {
            using var graph = new Graph();
            var finder = new Finder();
            using var route = new PlanarRoute(new PathfinderAdapter<Vector2>(finder));
            route.ConnectNode(ConnectionSide.From, graph.Nodes[0]);
            route.ConnectNode(ConnectionSide.To, graph.Nodes[1]);
            new DijkstraPathfinder().TryPathfind(graph.Nodes[0], new[] { graph.Nodes[3] }, out var candidate);
            switch (scenario)
            {
                case 0: candidate = null; break;
                case 1: candidate = new Path(graph.Nodes[1], Array.Empty<Arrow>()); break;
                case 2: candidate = new Path(graph.Nodes[0], Array.Empty<Arrow>()); break;
                case 3: graph.Edges[2].TraversalOptions = TraversalOptions.None; break;
                case 4: graph.Edges[2].DisconnectNode(ConnectionSide.To); break;
                case 5: graph.Edges[2].Dispose(); break;
            }
            finder.Handler = (_, _) => (true, candidate);
            Assert.That(() => route.ConnectNode(ConnectionSide.To, graph.Nodes[3]), Throws.Exception);
            Assert.That(route.To, Is.SameAs(graph.Nodes[1]));
            Assert.That(route.Views.Single().Source, Is.SameAs(graph.Edges[0]));
            Assert.That(HasSubscription(graph.Edges[0], route), Is.True);
            Assert.That(HasSubscription(graph.Edges[1], route), Is.False);
        }

        [Test]
        public void FromReplacementSearchesForwardAndTraversalDoesNotAutomaticallyRepath()
        {
            using var graph = new Graph();
            graph.Edges[0].TraversalOptions = TraversalOptions.Forward;
            graph.Edges[1].TraversalOptions = TraversalOptions.Backward;
            using var route = new PlanarRoute { TraversalOptions = TraversalOptions.None };
            route.ConnectNode(ConnectionSide.To, graph.Nodes[2]);
            Assert.That(route.TryConnectNode(ConnectionSide.From, graph.Nodes[0]), Is.True);
            Assert.That(route.TryConnectNode(ConnectionSide.From, graph.Nodes[1]), Is.True);
            var view = route.Views.Single();
            var updates = 0;
            route.EventHub.Connect(new object()).OnUpdated(_ => updates++);
            graph.Edges[1].TraversalOptions = TraversalOptions.None;
            Assert.That(route.Views.Single(), Is.SameAs(view));
            Assert.That(updates, Is.Zero);
            Assert.That(route.TryGetPosition(new Location(0.5f), out _), Is.True);
            Assert.That(route.TryConnectNode(ConnectionSide.From, graph.Nodes[0]), Is.False);
            Assert.That(route.From, Is.SameAs(graph.Nodes[1]));
            Assert.That(route.TraversalOptions, Is.EqualTo(TraversalOptions.None));
        }

        [Test]
        public void InvalidArgumentsAndDisposedEditsDoNotChangeEndpoints()
        {
            using var route = new PlanarRoute();
            using var node = new Planar.Node();
            using var generic = new Node<int>();
            using var genericSegment = new ScalarSegment();
            using var raw = new NavNode();
            using var disposed = new Planar.Node();
            disposed.Dispose();
            Assert.Throws<ArgumentNullException>(() => route.TryConnectNode(ConnectionSide.From, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => route.TryConnectNode((ConnectionSide)7, node));
            Assert.Throws<ArgumentOutOfRangeException>(() => route.DisconnectNode((ConnectionSide)7));
            Assert.Throws<ArgumentException>(() => route.TryConnectNode(ConnectionSide.From, raw));
            Assert.Throws<ArgumentException>(() => route.TryConnectNode(ConnectionSide.From, generic));
            Assert.Throws<ArgumentException>(() => route.Append(new MutableView { Source = genericSegment }));
            Assert.Throws<ArgumentException>(() => route.TryConnectNode(ConnectionSide.From, disposed));
            Assert.That(route.IsIsolated, Is.True);
            route.Dispose();
            Assert.Throws<ObjectDisposedException>(() => route.TryConnectNode(ConnectionSide.From, node));
            Assert.Throws<ObjectDisposedException>(() => route.DisconnectNode(ConnectionSide.From));
            Assert.Throws<ObjectDisposedException>(() => route.DisconnectNode(node));
        }

        [Test]
        public void CommonRouteSupportsNonPlanarGeometryAndReferenceDistinctNodes()
        {
            using var from = new EqualNode(0);
            using var to = new EqualNode(1);
            using var segment = new ScalarSegment();
            segment.ConnectNode(ConnectionSide.From, from);
            segment.ConnectNode(ConnectionSide.To, to);
            using var route = new Route();
            route.ConnectNode(ConnectionSide.From, from);
            route.ConnectNode(ConnectionSide.To, to);
            Assert.That(route.Nodes.Count(), Is.EqualTo(2));
            Assert.That(route.Views.Single().Source, Is.SameAs(segment));
            Assert.That(route.Length, Is.EqualTo(1f));
        }

        [Test]
        public void ViewEditingDropsStandaloneAnchorsAndReferenceCountsRepeatedSources()
        {
            using var graph = new Graph();
            using var route = new PlanarRoute();
            route.ConnectNode(ConnectionSide.From, graph.Nodes[5]);
            var view = new MutableView { Source = graph.Edges[0], IsForward = true };
            route.Append(view);
            view.Source = graph.Edges[3];
            view.IsForward = false;
            Assert.That(route.From, Is.SameAs(graph.Nodes[0]));
            graph.Nodes[5].Dispose();
            Assert.That(route.IsDisposed, Is.False);
            route.Append(new SegmentView(graph.Edges[0], false));
            Assert.That(HasSubscription(graph.Edges[0], route), Is.True);
            route.Remove(graph.Edges[0]);
            Assert.That(HasSubscription(graph.Edges[0], route), Is.True);
            var updates = 0;
            route.EventHub.Connect(new object()).OnUpdated(_ => updates++);
            graph.Nodes[0].Position = new Vector2(-1f, 0f);
            Assert.That(updates, Is.EqualTo(1));
            Assert.That(route.Length, Is.EqualTo(2f));
            route.Remove(graph.Edges[0]);
            Assert.That(route.IsIsolated, Is.True);
            Assert.That(HasSubscription(graph.Edges[0], route), Is.False);
            graph.Edges[0].Dispose();
            Assert.That(route.IsDisposed, Is.False);
        }

        [Test]
        public void CyclicPathAndCyclesThroughStandaloneEndpointsAreRejected()
        {
            using var graph = new Graph();
            var finder = new Finder();
            using var route = new PlanarRoute(new PathfinderAdapter<Vector2>(finder));
            route.ConnectNode(ConnectionSide.From, graph.Nodes[0]);
            route.ConnectNode(ConnectionSide.To, graph.Nodes[1]);
            var cyclic = new Path(graph.Nodes[0], new[]
            {
                new Arrow(route, graph.Nodes[1]),
                new Arrow(graph.Edges[1], graph.Nodes[2]),
            });
            finder.Handler = (_, _) => (true, cyclic);
            Assert.Throws<ArgumentException>(() => route.TryConnectNode(ConnectionSide.To, graph.Nodes[2]));
            Assert.That(route.To, Is.SameAs(graph.Nodes[1]));
            using var pin = new NodePin(graph.Nodes[0]);
            using var anchored = new PlanarRoute();
            anchored.ConnectNode(ConnectionSide.From, pin);
            using var onRoute = new SegmentPin(anchored);
            Assert.Throws<ArgumentException>(() => pin.Connect(onRoute));
            Assert.Throws<ArgumentException>(() => anchored.ConnectNode(ConnectionSide.To, onRoute));
            Assert.That(pin.AnchorNode, Is.SameAs(graph.Nodes[0]));
            Assert.That(anchored.To, Is.Null);
        }

        [Test]
        public void SubscriptionFailureRollsBackOnlyNewSubscriptions()
        {
            using var graph = new Graph();
            using var route = new PlanarRoute();
            route.ConnectNode(ConnectionSide.From, graph.Nodes[0]);
            route.ConnectNode(ConnectionSide.To, graph.Nodes[1]);
            graph.Edges[3].EventHub.Connect(route);
            Assert.Throws<InvalidOperationException>(() => route.ConnectNode(ConnectionSide.To, graph.Nodes[4]));
            Assert.That(route.To, Is.SameAs(graph.Nodes[1]));
            Assert.That(HasSubscription(graph.Edges[0], route), Is.True);
            Assert.That(HasSubscription(graph.Edges[1], route), Is.False);
            Assert.That(HasSubscription(graph.Edges[2], route), Is.False);
            Assert.That(HasSubscription(graph.Edges[3], route), Is.True);
            graph.Edges[3].EventHub.Disconnect(route);
            route.ConnectNode(ConnectionSide.To, graph.Nodes[4]);
            Assert.That(route.Views.Count, Is.EqualTo(4));
        }

        [Test]
        public void ObserverExceptionLeavesTheCompletedRouteAndSubscriptionsIntact()
        {
            using var graph = new Graph();
            using var route = new PlanarRoute();
            route.ConnectNode(ConnectionSide.From, graph.Nodes[0]);
            var listener = new object();
            route.EventHub.Connect(listener).OnUpdated(_ => throw new ApplicationException("Observer failed."));
            Assert.Throws<ApplicationException>(() => route.ConnectNode(ConnectionSide.To, graph.Nodes[2]));
            route.EventHub.Disconnect(listener);
            Assert.That(route.Views.Count, Is.EqualTo(2));
            Assert.That(route.To, Is.SameAs(graph.Nodes[2]));
            graph.Nodes[2].Position = new Vector2(3f, 0f);
            Assert.That(route.Length, Is.EqualTo(3f));
            graph.Edges[1].Dispose();
            Assert.That(route.IsDisposed, Is.True);
            Assert.That(graph.Nodes[0].IsDisposed, Is.False);
        }

        private bool HasSubscription(IReadOnlyNavSegment segment, Route route) =>
            ((EventHub<int>)segment.EventHub).HasListener(route);

        private class Graph : IDisposable
        {
            public Planar.Node[] Nodes { get; } = Enumerable.Range(0, 6)
                .Select(index => new Planar.Node(new Vector2(index, 0f))).ToArray();
            public Planar.LinearSegment[] Edges { get; }


            public Graph()
            {
                Edges = new[]
                {
                    new Planar.LinearSegment(Nodes[0], Nodes[1]),
                    new Planar.LinearSegment(Nodes[2], Nodes[1]),
                    new Planar.LinearSegment(Nodes[2], Nodes[3]),
                    new Planar.LinearSegment(Nodes[3], Nodes[4]),
                };
            }

            public void Dispose()
            {
                foreach (var edge in Edges) edge.Dispose();
                foreach (var node in Nodes) node.Dispose();
            }
        }

        private class Finder : IPathfinder
        {
            public int Calls { get; private set; }
            public IReadOnlyNavNode LastFrom { get; private set; }
            public IReadOnlyNavNode LastTo { get; private set; }
            public Func<IReadOnlyNavNode, IReadOnlyNavNode, (bool Success, Path Path)> Handler { get; set; }


            public bool TryPathfind(IReadOnlyNavNode from, IEnumerable<IReadOnlyNavNode> targets, out Path path)
            {
                Calls++;
                LastFrom = from;
                LastTo = targets.Single();
                if (Handler == null) return new DijkstraPathfinder().TryPathfind(from, new[] { LastTo }, out path);
                var result = Handler(from, LastTo);
                path = result.Path;
                return result.Success;
            }
        }

        private class WeightedSegment : Planar.Segment
        {
            public WeightedSegment(Planar.INode from, Planar.INode to, float length)
            {
                Length = length;
                ConnectNode(ConnectionSide.From, from);
                ConnectNode(ConnectionSide.To, to);
            }


            public override bool TryGetLocation(
                Vector2 position,
                float startDistance,
                float endDistance,
                out Location location,
                out double errorSquared,
                bool isForward = true,
                DistanceType distanceType = DistanceType.Length,
                float? epsilon = null)
            {
                location = default;
                errorSquared = default;
                return false;
            }

            public override bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null)
            {
                transform = default;
                return false;
            }
        }

        private class ScalarSegment : Segment<int, int>
        {
            public ScalarSegment() => Length = 1f;

            public override bool TryGetLocation(
                int position,
                float startDistance,
                float endDistance,
                out Location location,
                out double errorSquared,
                bool isForward = true,
                DistanceType distanceType = DistanceType.Length,
                float? epsilon = null)
            {
                location = default;
                errorSquared = default;
                return false;
            }
            public override bool TryGetTransform(
                Location location,
                out int position,
                out int rotation,
                float? epsilon = null)
            {
                position = rotation = 0;
                return false;
            }
        }

        private class EqualNode : Node<int>
        {
            public EqualNode(int position) : base(position) { }
            public override bool Equals(object other) => other is EqualNode;
            public override int GetHashCode() => 0;
        }

        private class MutableView : ISegmentView
        {
            public ISegment Source { get; set; }
            public bool IsForward { get; set; }
        }

        private class Payload : IDisposable
        {
            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }
    }
}
