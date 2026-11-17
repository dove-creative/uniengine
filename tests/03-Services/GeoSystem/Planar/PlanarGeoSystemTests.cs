using System;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem.Planar;
using NUnit.Framework;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("GeoSystemPlanar")]
    public class PlanarGeoSystemTests
    {
        [TestCase(true, DistanceType.Length, 2f, 2f, 0f)]
        [TestCase(false, DistanceType.Length, 2f, 8f, 180f)]
        [TestCase(true, DistanceType.Rate, 0.2f, 2f, 0f)]
        [TestCase(false, DistanceType.Rate, 0.2f, 8f, 180f)]
        public void LinearQueriesRespectDistanceDirectionAndDegrees(
            bool forward, DistanceType type, float distance, float x, float angle)
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var segment = new Planar.LinearSegment(from, to);
            AssertPose(segment, new Location(distance, type, forward), new Vector2(x, 0f), angle);
            to.Position = new Vector2(0f, 10f);
            AssertPose(segment, new Location(distance, type, forward), new Vector2(0f, x), angle + 90f);
        }

        [Test]
        public void ShapeEventsPropagateWhenLengthIsUnchanged()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var segment = new Planar.LinearSegment(from, to);
            using var pin = new SegmentPin(segment, new Location(0.5f, DistanceType.Rate));
            using var hold = new Hold();
            hold.MoveTo(pin);
            using var route = new Route();
            route.Append(new SegmentView(segment));
            var pinUpdates = 0;
            var routeUpdates = 0;
            pin.EventHub.Connect(new object()).OnUpdatedWhere(
                flags => (flags & (int)GeoEvents.Shape) != 0, () => pinUpdates++);
            route.EventHub.Connect(new object()).OnUpdated(_ => routeUpdates++);
            to.Position = new Vector2(0f, 10f);
            Assert.That(segment.Length, Is.EqualTo(10f));
            Assert.That(pin.Position, Is.EqualTo(new Vector2(0f, 5f)));
            Assert.That(hold.Position, Is.EqualTo(pin.Position));
            Assert.That(pinUpdates, Is.EqualTo(1));
            Assert.That(routeUpdates, Is.EqualTo(1));
            AssertPose(route, new Location(5f), new Vector2(0f, 5f), 90f);
        }

        [Test]
        public void BoundsDegeneracyAndNonFiniteInputsHaveExplicitFailureBehavior()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(Vector2.UnitX);
            using var segment = new Planar.LinearSegment(from, to);
            foreach (var value in new[] { -0.1f, 1.1f, float.NaN, float.PositiveInfinity })
            {
                Assert.That(segment.TryGetPosition(new Location(value), out var position), Is.False);
                Assert.That(position, Is.EqualTo(Vector2.Zero));
            }
            AssertPose(segment, new Location(-0.000005f), Vector2.Zero, 0f);
            Assert.That(segment.TryGetPosition(new Location(-0.000005f), out _, epsilon: 0f), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => segment.Epsilon = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => segment.TryGetPosition(default, out _, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => to.Position = new Vector2(float.NaN, 0f));
            Assert.That(to.Position, Is.EqualTo(Vector2.UnitX));
            to.Position = Vector2.Zero;
            Assert.That(segment.TryGetTransform(default, out _), Is.False);
            segment.DisconnectNode(to);
            Assert.That(segment.TryGetPosition(default, out _), Is.False);
            segment.Dispose();
            Assert.Throws<ObjectDisposedException>(() => segment.TryGetPosition(default, out _));
        }

        [Test]
        public void LayeredInterfacesExposeTheSameReferencesWithoutRecursiveGetters()
        {
            using var node = new Planar.Node(Vector2.Zero);
            using var pin = new NodePin(node);
            using var segment = new Planar.LinearSegment(node, pin);
            Assert.That(((INavNode)pin).Segments.Single(), Is.SameAs(segment));
            Assert.That(((INode)pin).Segments.Single(), Is.SameAs(segment));
            Assert.That(((IReadOnlyNode)pin).Segments.Single(), Is.SameAs(segment));
            Assert.That(((Planar.INode)pin).Position, Is.EqualTo(node.Position));
            Assert.That(((INavSegment)segment).From, Is.SameAs(node));
            Assert.That(((IReadOnlyNavSegment)segment).To, Is.SameAs(pin));
            Assert.That(((ISegment)segment).To, Is.SameAs(pin));
            Assert.That(((Planar.IReadOnlySegment)segment).Nodes, Is.EqualTo(new[] { node, (Planar.INode)pin }));
            using var rawNode = new NavNode();
            using var rawSegment = new NavSegment();
            using var empty = new Planar.LinearSegment();
            Assert.Throws<ArgumentException>(() => empty.ConnectNode(ConnectionSide.From, rawNode));
            Assert.Throws<ArgumentException>(() => rawSegment.ConnectNode(ConnectionSide.From, node));
            Assert.That(empty.IsIsolated && rawSegment.IsIsolated, Is.True);
        }

        [Test]
        public void AnchorRebindingDetachesOldSubscriptionsAndHoldOwnsOnlyItsPin()
        {
            using var first = new Planar.Node(Vector2.Zero);
            using var second = new Planar.Node(new Vector2(10f, 0f));
            using var pin = new NodePin(first);
            pin.Connect(second);
            first.Dispose();
            Assert.That(pin.IsDisposed, Is.False);
            second.Position = new Vector2(20f, 0f);
            Assert.That(pin.Position, Is.EqualTo(second.Position));
            using var segment = new Planar.LinearSegment(new Planar.Node(), second);
            using var hold = new Hold();
            hold.MoveTo(second);
            var previous = hold.Anchor;
            hold.MoveTo(segment, new Location(0.25f, DistanceType.Rate));
            Assert.That(previous.IsDisposed, Is.True);
            Assert.That(second.IsDisposed, Is.False);
            Assert.That(hold.Position, Is.EqualTo(new Vector2(5f, 0f)));
            var owned = hold.Anchor;
            hold.Dispose();
            Assert.That(owned.IsDisposed, Is.True);
            Assert.That(segment.IsDisposed, Is.False);
            segment.From.Dispose();
            pin.Disconnect();
            second.Dispose();
            Assert.That(pin.IsDisposed, Is.False);
            Assert.That(pin.IsValid, Is.False);
        }

        [Test]
        public void SegmentPinCanBecomeInvalidAndRecoverAfterSourceChanges()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var source = new Planar.LinearSegment(from, to);
            using var pin = new SegmentPin(source, new Location(15f));
            Assert.That(pin.IsValid, Is.False);
            to.Position = new Vector2(20f, 0f);
            Assert.That(pin.IsValid, Is.True);
            Assert.That(pin.Position, Is.EqualTo(new Vector2(15f, 0f)));
            pin.Location = new Location(5f, isForward: false);
            Assert.That(pin.Position, Is.EqualTo(new Vector2(15f, 0f)));
            source.DisconnectNode(to);
            Assert.That(pin.IsValid, Is.False);
            source.ConnectNode(ConnectionSide.To, to);
            Assert.That(pin.IsValid, Is.True);
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ClipDirectionUsesPhysicalPinOrderAndComposesWithQuery(bool clipForward, bool queryForward)
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var source = new Planar.LinearSegment(from, to);
            using var a = new SegmentPin(source, new Location(clipForward ? 0.2f : 0.8f, DistanceType.Rate));
            using var b = new SegmentPin(source, new Location(clipForward ? 0.2f : 0.8f, DistanceType.Rate, false));
            using var clip = new Clip(a, b);
            Assert.That(clip.IsForward, Is.EqualTo(clipForward));
            Assert.That(clip.Length, Is.EqualTo(6f).Within(1e-5f));
            var travelForward = clipForward == queryForward;
            AssertPose(clip, new Location(1f, isForward: queryForward),
                new Vector2(travelForward ? 3f : 7f, 0f), travelForward ? 0f : 180f);
            to.Position = new Vector2(20f, 0f);
            Assert.That(clip.Length, Is.EqualTo(12f).Within(1e-5f));
            AssertPose(clip, new Location(1f, isForward: queryForward),
                new Vector2(travelForward ? 5f : 15f, 0f), travelForward ? 0f : 180f);
        }

        [Test]
        public void ClipSourceIsDerivedAndInvalidStatesRecover()
        {
            using var a = new Planar.Node(Vector2.Zero);
            using var b = new Planar.Node(new Vector2(10f, 0f));
            using var c = new Planar.Node(new Vector2(0f, 10f));
            using var first = new Planar.LinearSegment(a, b);
            using var second = new Planar.LinearSegment(a, c);
            using var from = new SegmentPin(first, new Location(2f));
            using var to = new SegmentPin(second, new Location(8f));
            using var clip = new Clip(from, to);
            Assert.That(clip.SegmentSource, Is.Null);
            Assert.That(clip.TryGetPosition(default, out _), Is.False);
            to.Connect(first);
            Assert.That(clip.SegmentSource, Is.SameAs(first));
            Assert.That(clip.IsValid, Is.True);
            to.Location = from.Location;
            Assert.That(clip.IsValid, Is.False);
            to.Location = new Location(8f);
            Assert.That(clip.IsValid, Is.True);
            clip.Clear();
            Assert.That(clip.IsIsolated, Is.True);
            Assert.That(from.IsDisposed || to.IsDisposed || first.IsDisposed, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RouteQueriesComposeViewsAndLocationDirection(bool forward)
        {
            using var a = new Planar.Node(Vector2.Zero);
            using var b = new Planar.Node(new Vector2(10f, 0f));
            using var c = new Planar.Node(new Vector2(10f, 10f));
            using var first = new Planar.LinearSegment(a, b);
            using var second = new Planar.LinearSegment(c, b);
            using var route = new Route();
            route.Append(new SegmentView(second, false));
            route.Append(new SegmentView(first), toTail: false);
            Assert.That(route.Length, Is.EqualTo(20f));
            Assert.That(route.From, Is.SameAs(a));
            Assert.That(route.To, Is.SameAs(c));
            Assert.That(route.Nodes, Is.EquivalentTo(new[] { a, b, c }));
            AssertPose(route, new Location(5f, isForward: forward),
                forward ? new Vector2(5f, 0f) : new Vector2(10f, 5f), forward ? 0f : -90f);
            AssertPose(route, new Location(0.75f, DistanceType.Rate, forward),
                forward ? new Vector2(10f, 5f) : new Vector2(5f, 0f), forward ? 90f : 180f);
            AssertPose(route, new Location(10f, isForward: forward), new Vector2(10f, 0f), forward ? 0f : -90f);
            Assert.That(route.TryGetPosition(new Location(21f), out _), Is.False);
        }

        [Test]
        public void RouteCountsRepeatedSourcesAndClearReleasesOnlyItsReferences()
        {
            using var a = new Planar.Node(Vector2.Zero);
            using var b = new Planar.Node(new Vector2(10f, 0f));
            using var source = new Planar.LinearSegment(a, b);
            using var route = new Route();
            route.Append(new SegmentView(source));
            route.Append(new SegmentView(source, false));
            var updates = 0;
            route.EventHub.Connect(new object()).OnUpdated(_ => updates++);
            b.Position = new Vector2(20f, 0f);
            Assert.That(updates, Is.EqualTo(1));
            Assert.That(route.Length, Is.EqualTo(40f));
            route.Remove(source);
            b.Position = new Vector2(30f, 0f);
            Assert.That(route.Length, Is.EqualTo(30f));
            Assert.That(route.Views.Count, Is.EqualTo(1));
            route.ClearNodes();
            var before = updates;
            b.Position = new Vector2(40f, 0f);
            Assert.That(updates, Is.EqualTo(before));
            Assert.That(route.Views, Is.Empty);
            Assert.That(route.Length, Is.Zero);
            Assert.That(source.From, Is.SameAs(a));
            source.Dispose();
            Assert.That(route.IsDisposed, Is.False);
        }

        [Test]
        public void SourceDisposalPropagatesThroughPinsClipHoldAndRoute()
        {
            using var a = new Planar.Node(Vector2.Zero);
            using var b = new Planar.Node(new Vector2(10f, 0f));
            using var source = new Planar.LinearSegment(a, b);
            using var from = new SegmentPin(source, new Location(2f));
            using var to = new SegmentPin(source, new Location(8f));
            using var clip = new Clip(from, to);
            using var hold = new Hold();
            hold.MoveTo(source, new Location(5f));
            using var route = new Route();
            route.Append(new SegmentView(clip));
            source.Dispose();
            Assert.That(from.IsDisposed && to.IsDisposed, Is.True);
            Assert.That(clip.IsDisposed, Is.True);
            Assert.That(hold.IsDisposed, Is.True);
            Assert.That(route.IsDisposed, Is.True);
            Assert.That(a.IsDisposed || b.IsDisposed, Is.False);
            Assert.That(a.IsIsolated && b.IsIsolated, Is.True);
        }

        [Test]
        public void GeometricCyclesAreRejectedBeforeChangingAnchorsOrConnections()
        {
            using var a = new Planar.Node(Vector2.Zero);
            using var b = new Planar.Node(Vector2.UnitX);
            using var first = new NodePin(a);
            using var second = new NodePin(first);
            Assert.Throws<ArgumentException>(() => first.Connect(second));
            Assert.That(first.AnchorNode, Is.SameAs(a));
            using var segment = new Planar.LinearSegment(a, b);
            using var pin = new SegmentPin(segment);
            segment.DisconnectNode(b);
            Assert.Throws<ArgumentException>(() => segment.ConnectNode(ConnectionSide.To, pin));
            Assert.That(segment.To, Is.Null);
            using var route = new Route();
            using var outer = new Route();
            outer.Append(new SegmentView(route));
            Assert.Throws<ArgumentException>(() => route.Append(new SegmentView(outer)));
            Assert.That(route.Views, Is.Empty);
        }

        internal static void AssertPose(
            Planar.IReadOnlySegment segment,
            Location location,
            Vector2 position,
            float angle)
        {
            Assert.That(segment.TryGetTransform(location, out var transform), Is.True);
            Assert.That(Vector2.Distance(transform.Position, position), Is.LessThan(0.0001f));
            Assert.That(Math.IEEERemainder(transform.Rotation - angle, 360d), Is.EqualTo(0d).Within(0.0001d));
            Assert.That(segment.TryGetPosition(location, out var direct), Is.True);
            Assert.That(direct, Is.EqualTo(transform.Position));
        }
    }
}
