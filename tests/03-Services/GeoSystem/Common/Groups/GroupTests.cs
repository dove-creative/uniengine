using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("GeoGroups")]
    public class GroupTests
    {
        private class EqualNode : NavNode
        {
            public override bool Equals(object obj) => obj is EqualNode;
            public override int GetHashCode() => 0;
        }

        private class ExternalGroup : IReadOnlyNavGroup
        {
            public IReadOnlyList<IReadOnlyNavNode> Nodes => Array.Empty<IReadOnlyNavNode>();
            public IReadOnlyList<IReadOnlyNavSegment> Segments => Array.Empty<IReadOnlyNavSegment>();
            public IReadOnlyList<IReadOnlyNavGroup> NestedGroups { get; set; } = Array.Empty<IReadOnlyNavGroup>();
            public TrackingHub Hub { get; } = new();
            public ILifecycleHub LifecycleHub => Hub;
            public bool IsDisposed => Hub.IsDisposed;
            public void Dispose() => Hub.Dispose();
        }

        private class TrackingHub : ILifecycleHub, IDisposable
        {
            private readonly LifecycleHub _hub = new();
            private readonly HashSet<object> _listeners = new();
            public bool IsDisposed => _hub.IsDisposed;
            public int Count => _listeners.Count;
            public bool FailConnect { get; set; }

            public ILifecycleHubBuilder Connect(object listener)
            {
                _listeners.Add(listener);
                if (FailConnect) throw new InvalidOperationException("Subscription failed.");
                return _hub.Connect(listener);
            }

            public void Disconnect(object listener)
            {
                _listeners.Remove(listener);
                _hub.Disconnect(listener);
            }

            public void Dispose()
            {
                _hub.Dispose();
                _listeners.Clear();
            }
        }

        [Test]
        public void NavMembershipUsesIdentityAndDoesNotEditTheGraph()
        {
            using var group = new NavGroup();
            using var first = new EqualNode();
            using var second = new EqualNode();
            using var segment = new NavSegment(first, second);
            Assert.That(group.AddSegment(segment), Is.True);
            Assert.That(group.Nodes, Is.Empty);
            Assert.That(group.AddNode(first), Is.True);
            Assert.That(group.AddNode(second), Is.True);
            Assert.That(group.AddNode(first), Is.False);
            Assert.That(group.Nodes, Has.Count.EqualTo(2));
            Assert.That(group.Nodes[0], Is.SameAs(first));
            Assert.That(group.Nodes[1], Is.SameAs(second));
            Assert.Throws<NotSupportedException>(() => ((IList<INavNode>)group.Nodes).Clear());
            Assert.That(group.RemoveNode(first), Is.True);
            Assert.That(group.Nodes.Single(), Is.SameAs(second));
            Assert.That(group.RemoveNode(first), Is.False);
            Assert.That(group.RemoveNode(null), Is.False);
            group.Clear();
            Assert.That(segment.From, Is.SameAs(first));
            Assert.That(segment.To, Is.SameAs(second));
            Assert.That(first.Segments.Single(), Is.SameAs(segment));
            Assert.That(first.IsDisposed || second.IsDisposed || segment.IsDisposed, Is.False);
        }

        [Test]
        public void AllGeometryViewsExposeTheSameLiveCollections()
        {
            using var group = new GeoGroup<float, bool>();
            using var child = new GeoGroup<float, bool>();
            using var from = new Node<float>(0f);
            using var to = new Node<float>(10f);
            using var segment = new GenericRouteTests.ScalarSegment(from, to);
            IReadOnlyGeoGroup<float, bool> full = group;
            IReadOnlyGeoGroup<float> position = full;
            IReadOnlyNavGroup navigation = position;
            var captured = navigation.Nodes;
            group.AddNode(from);
            group.AddSegment(segment);
            group.AddGroup(child);
            Assert.That(full.Nodes, Is.SameAs(position.Nodes));
            Assert.That(position.Nodes, Is.SameAs(captured));
            Assert.That(full.Segments, Is.SameAs(position.Segments));
            Assert.That(position.Segments, Is.SameAs(navigation.Segments));
            Assert.That(full.NestedGroups, Is.SameAs(navigation.NestedGroups));
            Assert.That(captured.Single(), Is.SameAs(from));
            Assert.That((object)group, Is.Not.InstanceOf<INavGroup>());
            from.Dispose();
            Assert.That(captured, Is.Empty);
            segment.Dispose();
            Assert.That(navigation.Segments, Is.Empty);
            child.Dispose();
            Assert.That(navigation.NestedGroups, Is.Empty);
        }

        [Test]
        public void RouteViewsProjectOnlyRegisteredHoldsAndRoutes()
        {
            using var group = new RouteGroup<float, bool>();
            using var child = new RouteGroup<float, bool>();
            using var from = new Node<float>(0f);
            using var to = new Node<float>(10f);
            using var source = new GenericRouteTests.ScalarSegment(from, to);
            using var hold = new Hold<float, bool>();
            using var route = new Route<float, bool>();
            hold.MoveTo(source, new Location(3f));
            route.Append(new SegmentView(source));
            group.AddHold(hold);
            group.AddRoute(route);
            group.AddGroup(child);
            IReadOnlyRouteGroup<float, bool> routes = group;
            IReadOnlyGeoGroup<float, bool> geometry = routes;
            IReadOnlyGeoGroup<float> positions = geometry;
            IReadOnlyNavGroup navigation = positions;
            Assert.That(routes.Holds, Is.SameAs(geometry.Nodes));
            Assert.That(routes.Routes, Is.SameAs(geometry.Segments));
            Assert.That(routes.Routes, Is.SameAs(navigation.Segments));
            Assert.That(routes.NestedGroups, Is.SameAs(positions.NestedGroups));
            Assert.That(routes.NestedGroups, Is.SameAs(navigation.NestedGroups));
            Assert.That(navigation.Nodes, Is.EqualTo(new[] { hold }));
            Assert.That(navigation.Segments, Is.EqualTo(new[] { route }));
            Assert.That((object)group, Is.Not.InstanceOf<IGeoGroup<float, bool>>());
            Assert.That((object)group, Is.Not.InstanceOf<INavGroup>());
            Assert.Throws<NotSupportedException>(() => ((IList<Hold<float, bool>>)routes.Holds).Clear());
            source.Dispose();
            Assert.That(group.Holds, Is.Empty);
            Assert.That(group.Routes, Is.Empty);
            Assert.That(group.NestedGroups.Single(), Is.SameAs(child));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void RemovalAndGroupDisposalNeverOwnMembers(int kind)
        {
            using var node = new Node<float>(0f);
            using var other = new Node<float>(10f);
            using var segment = new GenericRouteTests.ScalarSegment(node, other);
            using var hold = new Hold<float, bool>();
            using var route = new Route<float, bool>();
            hold.MoveTo(node);
            route.Append(new SegmentView(segment));
            using var nav = new NavGroup();
            using var geo = new GeoGroup<float, bool>();
            using var routes = new RouteGroup<float, bool>();
            using var navChild = new NavGroup();
            using var geoChild = new GeoGroup<float, bool>();
            using var routeChild = new RouteGroup<float, bool>();
            if (kind == 0)
            {
                nav.AddNode(node);
                nav.AddSegment(segment);
                nav.AddGroup(navChild);
                nav.Dispose();
                Assert.That(nav.Nodes, Is.Empty);
                Assert.That(nav.NestedGroups, Is.Empty);
                Assert.Throws<ObjectDisposedException>(() => nav.AddNode(node));
            }
            else if (kind == 1)
            {
                geo.AddNode(node);
                geo.AddSegment(segment);
                geo.AddGroup(geoChild);
                geo.Clear();
                geo.AddNode(node);
                geo.Dispose();
                Assert.That(geo.Nodes, Is.Empty);
                Assert.Throws<ObjectDisposedException>(() => geo.AddNode(node));
            }
            else
            {
                routes.AddHold(hold);
                routes.AddRoute(route);
                routes.AddGroup(routeChild);
                Assert.That(routes.RemoveHold(hold), Is.True);
                Assert.That(routes.RemoveRoute(route), Is.True);
                routes.AddHold(hold);
                routes.AddRoute(route);
                routes.Dispose();
                Assert.That(routes.Holds, Is.Empty);
                Assert.That(routes.Routes, Is.Empty);
                Assert.Throws<ObjectDisposedException>(() => routes.AddHold(hold));
            }
            Assert.That(new ILifecycleSubject[]
                { node, other, segment, hold, route, navChild, geoChild, routeChild }
                .Any(item => item.IsDisposed), Is.False);
            Assert.That(segment.From, Is.SameAs(node));
            Assert.That(segment.To, Is.SameAs(other));
        }

        [Test]
        public void SharedHierarchyDeduplicatesTraversalAndRejectsCycles()
        {
            using var root = new GeoGroup<float, bool>();
            using var first = new GeoGroup<float, bool>();
            using var second = new GeoGroup<float, bool>();
            using var shared = new RouteGroup<float, bool>();
            using var node = new Node<float>(0f);
            using var hold = new Hold<float, bool>();
            using var route = new Route<float, bool>();
            root.AddNode(node);
            first.AddNode(node);
            shared.AddHold(hold);
            shared.AddRoute(route);
            root.AddGroup(first);
            root.AddGroup(second);
            first.AddGroup(shared);
            second.AddGroup(shared);
            Assert.That(root.EnumerateNodes(), Is.EqualTo(new INode<float>[] { node, hold }));
            Assert.That(root.EnumerateSegments(), Is.EqualTo(new[] { route }));
            Assert.That(root.EnumerateDescendants(), Is.EqualTo(new IReadOnlyGeoGroup<float, bool>[]
                { first, shared, second }));
            Assert.That(((IReadOnlyNavGroup)root).EnumerateNodes(), Is.EqualTo(new INavNode[] { node, hold }));
            Assert.That(((IReadOnlyGeoGroup<float>)root).EnumerateNodes().Count(), Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => root.AddGroup(root));
            Assert.Throws<ArgumentException>(() => first.AddGroup(root));
            Assert.That(first.NestedGroups, Is.EqualTo(new[] { shared }));
            shared.Dispose();
            Assert.That(first.NestedGroups, Is.Empty);
            Assert.That(second.NestedGroups, Is.Empty);
            Assert.That(root.EnumerateNodes(), Is.EqualTo(new[] { node }));
            Assert.That(hold.IsDisposed || route.IsDisposed, Is.False);
        }

        [Test]
        public void SharedRouteGroupsKeepTypedRecursiveViews()
        {
            using var root = new RouteGroup<float, bool>();
            using var child = new RouteGroup<float, bool>();
            using var hold = new Hold<float, bool>();
            using var route = new Route<float, bool>();
            root.AddHold(hold);
            child.AddHold(hold);
            child.AddRoute(route);
            root.AddGroup(child);
            Assert.That(root.EnumerateHolds(), Is.EqualTo(new[] { hold }));
            Assert.That(root.EnumerateRoutes(), Is.EqualTo(new[] { route }));
            Assert.That(root.EnumerateDescendants(), Is.EqualTo(new[] { child }));
            Assert.Throws<ArgumentException>(() => child.AddGroup(root));
            hold.Dispose();
            Assert.That(root.Holds, Is.Empty);
            Assert.That(child.Holds, Is.Empty);
        }

        [Test]
        public void SubscriptionsAreReleasedOnEveryRemovalPathAndFailure()
        {
            using var first = new NavGroup();
            using var second = new NavGroup();
            using var child = new ExternalGroup();
            first.AddGroup(child);
            second.AddGroup(child);
            Assert.That(child.Hub.Count, Is.EqualTo(2));
            Assert.That(first.AddGroup(child), Is.False);
            Assert.That(child.Hub.Count, Is.EqualTo(2));
            first.RemoveGroup(child);
            Assert.That(child.Hub.Count, Is.EqualTo(1));
            first.AddGroup(child);
            first.Clear();
            Assert.That(child.Hub.Count, Is.EqualTo(1));
            second.Dispose();
            Assert.That(child.Hub.Count, Is.Zero);
            child.Hub.FailConnect = true;
            Assert.Throws<InvalidOperationException>(() => first.AddGroup(child));
            Assert.That(first.NestedGroups, Is.Empty);
            Assert.That(child.Hub.Count, Is.Zero);
        }

        [Test]
        public void InvalidMembersAndExternalCyclesLeaveGroupUnchanged()
        {
            using var group = new NavGroup();
            using var node = new NavNode();
            using var child = new ExternalGroup();
            Assert.Throws<ArgumentNullException>(() => group.AddNode(null));
            Assert.Throws<ArgumentNullException>(() => group.AddGroup(null));
            node.Dispose();
            Assert.Throws<ArgumentException>(() => group.AddNode(node));
            child.NestedGroups = new[] { child };
            Assert.Throws<ArgumentException>(() => group.AddGroup(child));
            child.NestedGroups = Array.Empty<IReadOnlyNavGroup>();
            child.Dispose();
            Assert.Throws<ArgumentException>(() => group.AddGroup(child));
            Assert.That(group.Nodes, Is.Empty);
            Assert.That(group.NestedGroups, Is.Empty);
        }
    }
}
