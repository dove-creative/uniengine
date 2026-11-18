using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem;
using BlackThunder.GeoSystem.RouteSystem.Planar;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;
using Route = BlackThunder.GeoSystem.RouteSystem.Planar.Route;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("GeoSystemPlanar")]
    [Category("Pathfinding")]
    public class NavigationContractsTests
    {
        [Test]
        public void PropertiesAreIndependentAcrossInterfacesAndSurviveOwnerDisposal()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var source = new Planar.LinearSegment(from, to);
            using var nodePin = new NodePin(from);
            using var first = new SegmentPin(source, new Location(2f));
            using var last = new SegmentPin(source, new Location(8f));
            using var clip = new Clip(first, last);
            using var hold = new Hold();
            hold.MoveTo(first);
            using var route = new Route();
            route.Append(new SegmentView(source));
            using var rawNode = new NavNode();
            using var rawSegment = new NavSegment();
            IPropertySubject[] subjects =
            {
                rawNode, rawSegment, from, to, source, nodePin, first, last, clip, hold,
                (IReadOnlyNavNode)hold.Anchor, route,
            };
            var bags = new HashSet<Property>();
            var payloads = new List<Payload>();
            var updates = 0;
            foreach (var subject in subjects)
            {
                ((IEventSubject<int>)subject).EventHub.Connect(new object()).OnUpdated(_ => updates++);
                var bag = subject.Property;
                Assert.That(bags.Add(bag), Is.True);
                Assert.That(subject.Property, Is.SameAs(bag));
                if (subject is IReadOnlyNavNode node) Assert.That(node.Property, Is.SameAs(bag));
                if (subject is IReadOnlyNavSegment segment) Assert.That(segment.Property, Is.SameAs(bag));
                if (subject is IReadOnlyNode<Vector2> positioned) Assert.That(positioned.Property, Is.SameAs(bag));
                if (subject is IReadOnlySegment<Vector2> geometric) Assert.That(geometric.Property, Is.SameAs(bag));
                var payload = new Payload();
                payloads.Add(payload);
                bag.Add("payload", payload);
            }
            Assert.That(updates, Is.Zero);
            foreach (var subject in subjects) ((IDisposable)subject).Dispose();
            for (int i = 0; i < subjects.Length; i++)
            {
                var bag = subjects[i].Property;
                Assert.That(bags.Contains(bag), Is.True);
                Assert.That(bag.Get("payload"), Is.SameAs(payloads[i]));
                Assert.That(payloads[i].DisposeCount, Is.Zero);
                bag.Remove("payload");
                payloads[i].Dispose();
                Assert.That(bag, Is.Empty);
            }
        }

        [TestCase(typeof(NavNode))]
        [TestCase(typeof(NavSegment))]
        [TestCase(typeof(Route))]
        public void PropertyStorageIsLazyEvenWhenFirstAccessFollowsDisposal(Type type)
        {
            IPropertySubject subject = type == typeof(NavSegment)
                ? new NavSegment() : (IPropertySubject)Activator.CreateInstance(type);
            using var lifetime = (IDisposable)subject;
            // Inspect storage only for the allocation contract, including untouched disposal.
            var storageType = type == typeof(Route) ? typeof(BlackThunder.GeoSystem.RouteSystem.Route) : type;
            var storage = storageType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(Property));
            Assert.That(storage.GetValue(subject), Is.Null);
            lifetime.Dispose();
            Assert.That(storage.GetValue(subject), Is.Null);
            var bag = subject.Property;
            Assert.That(bag, Is.Not.Null);
            Assert.That(subject.Property, Is.SameAs(bag));
            Assert.That(storage.GetValue(subject), Is.SameAs(bag));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TraversalChangesNotifyOnceAfterStateUpdateAndRejectInvalidEdits(bool useRoute)
        {
            using INavSegment segment = useRoute ? new Route() : new NavSegment();
            IReadOnlyNavSegment readOnly = segment;
            var events = new List<int>();
            Assert.That(readOnly.TraversalOptions, Is.EqualTo(TraversalOptions.Both));
            segment.EventHub.Connect(new object()).OnUpdated(flags =>
            {
                Assert.That(readOnly.TraversalOptions, Is.EqualTo(TraversalOptions.Forward));
                events.Add(flags);
            });
            segment.TraversalOptions = TraversalOptions.Forward;
            segment.TraversalOptions = TraversalOptions.Forward;
            Assert.That(events, Is.EqualTo(new[] { (int)GeoEvents.Traversal }));
            foreach (var invalid in new[] { (TraversalOptions)4, (TraversalOptions)(-1) })
                Assert.Throws<ArgumentOutOfRangeException>(() => segment.TraversalOptions = invalid);
            Assert.That(readOnly.TraversalOptions, Is.EqualTo(TraversalOptions.Forward));
            Assert.That(events.Count, Is.EqualTo(1));
            segment.Dispose();
            Assert.Throws<ObjectDisposedException>(() => segment.TraversalOptions = TraversalOptions.Forward);
            Assert.That((int)GeoEvents.Traversal, Is.EqualTo(16));
            Assert.That((int)RouteEvents.Traversal, Is.EqualTo(16));
            Assert.That((int)RouteEvents.Location, Is.EqualTo(8));
            Assert.That(GeoEvents.All & GeoEvents.Traversal, Is.EqualTo(GeoEvents.Traversal));
            Assert.That(RouteEvents.All & RouteEvents.Traversal, Is.EqualTo(RouteEvents.Traversal));
        }

        [Test]
        public void SourceTraversalDoesNotPropagateShapeOrOverrideIndependentOptions()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var source = new Planar.LinearSegment(from, to);
            using var first = new SegmentPin(source, new Location(2f));
            using var last = new SegmentPin(source, new Location(8f));
            using var clip = new Clip(first, last) { TraversalOptions = TraversalOptions.Forward };
            using var hold = new Hold();
            hold.MoveTo(first);
            using var route = new Route { TraversalOptions = TraversalOptions.Backward };
            route.Append(new SegmentView(source, false));
            var dependentUpdates = 0;
            IEventSubject<int>[] dependents = { first, last, clip, hold, route };
            foreach (var dependent in dependents)
                dependent.EventHub.Connect(new object()).OnUpdated(_ => dependentUpdates++);

            source.TraversalOptions = TraversalOptions.None;
            Assert.That(dependentUpdates, Is.Zero);
            Assert.That(clip.TraversalOptions, Is.EqualTo(TraversalOptions.Forward));
            Assert.That(route.TraversalOptions, Is.EqualTo(TraversalOptions.Backward));
            Assert.That(source.TryGetTransform(new Location(5f), out var transform), Is.True);
            Assert.That(transform.Position, Is.EqualTo(new Vector2(5f, 0f)));
            Assert.That(clip.TryGetPosition(new Location(1f), out var position), Is.True);
            Assert.That(position, Is.EqualTo(new Vector2(3f, 0f)));
            Assert.That(route.TryGetPosition(new Location(1f), out position), Is.True);
            Assert.That(position, Is.EqualTo(new Vector2(9f, 0f)));
            Assert.That(hold.Position, Is.EqualTo(first.Position));

            var search = new AStarPathfinder<Vector2>(Vector2.Distance);
            Assert.That(search.TryPathfind(first, new[] { last }, out var path), Is.True);
            Assert.That(path.Arrows.Single().Segment, Is.SameAs(clip));
            clip.TraversalOptions = TraversalOptions.None;
            Assert.That(search.TryPathfind(first, new[] { last }, out _), Is.False);
            Assert.That(clip.TryGetPosition(new Location(1f), out _), Is.True);
            route.TraversalOptions = TraversalOptions.None;
            Assert.That(route.TryGetTransform(new Location(1f), out _), Is.True);

            clip.TraversalOptions = TraversalOptions.Forward;
            first.Location = new Location(9f);
            Assert.That(clip.IsForward, Is.False);
            Assert.That(clip.TraversalOptions, Is.EqualTo(TraversalOptions.Forward));
            source.DisconnectNode(ConnectionSide.To);
            source.ConnectNode(ConnectionSide.To, to);
            Assert.That(source.TraversalOptions, Is.EqualTo(TraversalOptions.None));
            Assert.That(clip.IsValid, Is.True);
        }

        [Test]
        public void DistanceHeuristicReturnsTypedPlanarReferencesThroughBothInterfaces()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var near = new Planar.Node(Vector2.UnitX);
            using var far = new Planar.Node(new Vector2(0f, 10f));
            using var goal = new Planar.Node(new Vector2(2f, 0f));
            using var a = new Planar.LinearSegment(from, far);
            using var b = new Planar.LinearSegment(far, goal);
            using var c = new Planar.LinearSegment(from, near);
            using var d = new Planar.LinearSegment(near, goal);
            IPathfinder<Vector2> typed = new AStarPathfinder<Vector2>(Vector2.Distance);
            Assert.That(typed.TryPathfind(from, new[] { goal, far }, out var path), Is.True);
            Assert.That(path.Arrows.Select(arrow => arrow.Segment), Is.EqualTo(new[] { c, d }));
            IPathfinder raw = typed;
            Assert.That(raw.TryPathfind(from, new[] { goal, far }, out var untyped), Is.True);
            Assert.That(untyped, Is.InstanceOf<Path<Vector2>>());
            Assert.That(untyped.From, Is.SameAs(path.From));
            Assert.That(untyped.Arrows.Select(arrow => arrow.Segment), Is.EqualTo(new[] { c, d }));
        }

        private class Payload : IDisposable
        {
            public int DisposeCount { get; private set; }
            public void Dispose() => DisposeCount++;
        }
    }
}
