using System;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem;
using NUnit.Framework;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("LocationQuery")]
    public class LocationQueryTests
    {
        [TestCase(true, DistanceType.Length, 2f)]
        [TestCase(false, DistanceType.Length, 8f)]
        [TestCase(true, DistanceType.Rate, 0.2f)]
        [TestCase(false, DistanceType.Rate, 0.8f)]
        public void RequestedRepresentationPreservesPositionAndControlsRotation(
            bool forward, DistanceType type, float expected)
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var line = new Planar.LinearSegment(from, to);
            IReadOnlySegment<Vector2> query = line;
            Assert.That(query.TryGetLocation(new Vector2(2f, 0f), out var location, forward, type), Is.True);
            Assert.That(location, Is.EqualTo(new Location(expected, type, forward)));
            Assert.That(line.TryGetTransform(location, out var position, out var angle), Is.True);
            Assert.That(Vector2.Distance(position, new Vector2(2f, 0f)), Is.LessThan(0.00001f));
            Assert.That(angle, Is.EqualTo(forward ? 0f : 180f));
            Assert.That(query.TryGetLocation(new Vector2(2f, 0f), out location), Is.True);
            Assert.That(location, Is.EqualTo(new Location(2f)));
        }

        [TestCase(2f, 8f)]
        [TestCase(8f, 2f)]
        [TestCase(2f, 2f)]
        public void BoundedProjectionIncludesEndpointsAndReportsSpatialError(float start, float end)
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var line = new Planar.LinearSegment(from, to);
            var position = new Vector2(1.875f, 0f);
            Assert.That(line.TryGetLocation(position, start, end, out var location, out var error,
                epsilon: 0.125f), Is.True);
            Assert.That(location.Distance, Is.EqualTo(2f));
            Assert.That(error, Is.EqualTo(0.015625d));
            Assert.That(line.TryGetLocation(position, start, end, out location, out error, epsilon: 0.1f), Is.False);
            Assert.That(location, Is.EqualTo(default(Location)));
            Assert.That(error, Is.Zero);
        }

        [Test]
        public void SpatialToleranceIncludesItsBoundaryAndCanBeOverridden()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var line = new Planar.LinearSegment(from, to) { Epsilon = 0.125f };
            Assert.That(line.TryGetLocation(new Vector2(2f, 0.125f), out _), Is.True);
            Assert.That(line.TryGetLocation(new Vector2(2f, 0.126f), out _), Is.False);
            Assert.That(line.TryGetLocation(new Vector2(-0.125f, 0f), out var location), Is.True);
            Assert.That(location.Distance, Is.Zero);
            Assert.That(line.TryGetLocation(new Vector2(2f, 0.125f), out _, epsilon: 0f), Is.False);
            Assert.That(line.TryGetLocation(new Vector2(10f, 0f), out location, epsilon: 0f), Is.True);
            Assert.That(location.Distance, Is.EqualTo(10f));
            Assert.That(line.TryGetLocation(Vector2.Zero, out location, false, DistanceType.Rate), Is.True);
            Assert.That(location.Distance, Is.EqualTo(1f));
        }

        [Test]
        public void InvalidArgumentsAndInvalidGeometryHaveDistinctFailureContracts()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var line = new Planar.LinearSegment(from, to);
            foreach (var value in new[] { -1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    line.TryGetLocation(Vector2.Zero, out _, epsilon: value));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                line.TryGetLocation(Vector2.Zero, out _, distanceType: (DistanceType)99));
            foreach (var value in new[] { -1f, 11f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    line.TryGetLocation(Vector2.Zero, value, 10f, out _, out _));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    line.TryGetLocation(Vector2.Zero, 0f, value, out _, out _));
            }
            foreach (var value in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            {
                Assert.That(line.TryGetLocation(new Vector2(value, 0f), out var location), Is.False);
                Assert.That(location, Is.EqualTo(default(Location)));
                Assert.That(line.TryGetLocation(new Vector2(0f, value), 0f, 10f,
                    out location, out var error), Is.False);
                Assert.That(error, Is.Zero);
            }
            to.Position = Vector2.Zero;
            Assert.That(line.TryGetLocation(Vector2.Zero, out _), Is.False);
            to.Position = new Vector2(0.000001f, 0f);
            Assert.That(line.TryGetLocation(Vector2.Zero, out _), Is.False);
            Assert.That(line.TryGetLocation(Vector2.Zero, out _, epsilon: 0f), Is.True);
            line.DisconnectNode(to);
            Assert.That(line.TryGetLocation(Vector2.Zero, out _), Is.False);
            line.Dispose();
            Assert.Throws<ObjectDisposedException>(() => line.TryGetLocation(Vector2.Zero, out _));
            Assert.Throws<ObjectDisposedException>(() => line.TryGetLocation(Vector2.Zero, 0f, 0f, out _, out _));
        }

        [Test]
        public void LargeCoordinatesUseFiniteDoubleProjectionAndError()
        {
            using var from = new Planar.Node(new Vector2(-1e20f, 0f));
            using var to = new Planar.Node(new Vector2(1e20f, 0f));
            using var line = new Planar.LinearSegment(from, to);
            var position = new Vector2(0f, 1e18f);
            Assert.That(line.TryGetLocation(position, 0f, line.Length, out var location, out var error,
                distanceType: DistanceType.Rate, epsilon: 2e18f), Is.True);
            Assert.That(location.Distance, Is.EqualTo(0.5f));
            Assert.That(error, Is.EqualTo((double)position.Y * position.Y));
            from.Position = new Vector2(-float.MaxValue, 0f);
            to.Position = new Vector2(float.MaxValue, 0f);
            Assert.That(line.TryGetLocation(Vector2.Zero, out _), Is.False);
        }

        [Test]
        public void OverlapSelectionDependsOnSearchOrderButNotOutputDirection()
        {
            using var scene = new OverlapScene();
            var point = new Vector2(3f, 0f);
            Assert.That(scene.Segment.TryGetLocation(point, out var location, isForward: false), Is.True);
            Assert.That(location.Distance, Is.EqualTo(27f));
            Assert.That(scene.Segment.TryGetLocation(point, 30f, 0f, out location, out var error), Is.True);
            Assert.That(location.Distance, Is.EqualTo(23f));
            Assert.That(error, Is.Zero);
            Assert.That(scene.Segment.TryGetLocation(point, 12f, 28f, out location, out _), Is.True);
            Assert.That(location.Distance, Is.EqualTo(17f));
            Assert.That(scene.Segment.TryGetLocation(point, 28f, 12f, out location, out _), Is.True);
            Assert.That(location.Distance, Is.EqualTo(23f));
        }

        [Test]
        public void CrossingAndHingeQueriesUseTheActualPolyline()
        {
            using var from = new Planar.Node(new Vector2(-2f, -2f));
            using var to = new Planar.Node(new Vector2(2f, -2f));
            using var first = new Planar.ControlHandle(new Vector2(2f, 2f));
            using var second = new Planar.ControlHandle(new Vector2(-2f, 2f));
            using var poly = new Planar.PolySegment(from, to);
            poly.Values[ShaperValues.TargetSegmentLength] = 100f;
            poly.Handles.Add(first);
            poly.Handles.Add(second);
            Assert.That(poly.TryGetLocation(Vector2.Zero, out var location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(Math.Sqrt(8)).Within(0.00001));
            Assert.That(poly.TryGetLocation(Vector2.Zero, poly.Length, 0f, out location, out _), Is.True);
            Assert.That(location.Distance, Is.EqualTo(poly.Length - Math.Sqrt(8)).Within(0.00001));
            Assert.That(poly.TryGetLocation(first.Position, out location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(Math.Sqrt(32)).Within(0.00001));
            Assert.That(poly.TryGetPosition(location, out var point), Is.True);
            Assert.That(point, Is.EqualTo(first.Position));
        }

        [Test]
        public void ClosestCandidateWinsAcrossBothPolylinePiecesAndRouteChildren()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var lower = new Planar.Node(new Vector2(10f, 0f));
            using var upper = new Planar.Node(new Vector2(10f, 1f));
            using var to = new Planar.Node(new Vector2(0f, 1f));
            using var first = new Planar.LinearSegment(from, lower);
            using var middle = new Planar.LinearSegment(lower, upper);
            using var last = new Planar.LinearSegment(upper, to);
            using var route = new Route<Vector2, float>();
            route.Append(new SegmentView(first));
            route.Append(new SegmentView(middle));
            route.Append(new SegmentView(last));
            using var handle1 = new Planar.ControlHandle(lower.Position);
            using var handle2 = new Planar.ControlHandle(upper.Position);
            using var poly = new Planar.PolySegment(from, to);
            poly.Handles.Add(handle1);
            poly.Handles.Add(handle2);
            foreach (IReadOnlySegment<Vector2> query in new IReadOnlySegment<Vector2>[] { poly, route })
            {
                Assert.That(query.TryGetLocation(new Vector2(5f, 0.75f), 0f, query.Length,
                    out var location, out var error, epsilon: 0.8f), Is.True);
                Assert.That(location.Distance, Is.EqualTo(16f));
                Assert.That(error, Is.EqualTo(0.0625d));
                Assert.That(query.TryGetLocation(new Vector2(5f, 0.5f), out location, epsilon: 0.8f), Is.True);
                Assert.That(location.Distance, Is.EqualTo(5f));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClipFindsAnInRangeOccurrenceEvenWhenTheSourceFirstMatchIsOutside(bool reverse)
        {
            using var scene = new OverlapScene();
            using var from = new SegmentPin<Vector2, float>(scene.Segment, new Location(reverse ? 28f : 12f));
            using var to = new SegmentPin<Vector2, float>(scene.Segment, new Location(reverse ? 12f : 28f));
            using var clip = new Clip<Vector2, float>(from, to);
            Assert.That(clip.TryGetLocation(new Vector2(3f, 0f), out var location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(5f));
            Assert.That(clip.TryGetLocation(new Vector2(3f, 0f), 16f, 0f, out location, out _), Is.True);
            Assert.That(location.Distance, Is.EqualTo(11f));
            Assert.That(clip.TryGetPosition(location, out var point), Is.True);
            Assert.That(point, Is.EqualTo(new Vector2(3f, 0f)));
        }

        [Test]
        public void NestedReversedAndRepeatedRoutesUseTheirOwnDistanceAndSearchOrder()
        {
            using var scene = new OverlapScene();
            using var inner = new Route<Vector2, float>();
            using var outer = new Route<Vector2, float>();
            inner.Append(new SegmentView(scene.Segment, false));
            outer.Append(new SegmentView(inner));
            outer.Append(new SegmentView(inner, false));
            var point = new Vector2(3f, 0f);
            Assert.That(outer.TryGetLocation(point, out var location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(7f));
            Assert.That(outer.TryGetLocation(point, out location, false, DistanceType.Rate), Is.True);
            Assert.That(location.Distance, Is.EqualTo(53f / 60f));
            Assert.That(outer.TryGetLocation(point, 60f, 0f, out location, out _), Is.True);
            Assert.That(location.Distance, Is.EqualTo(53f));
            Assert.That(outer.TryGetLocation(point, 31f, 50f, out location, out _), Is.True);
            Assert.That(location.Distance, Is.EqualTo(33f));
            using var start = new SegmentPin<Vector2, float>(outer, new Location(31f));
            using var end = new SegmentPin<Vector2, float>(outer, new Location(50f));
            using var clip = new Clip<Vector2, float>(start, end);
            Assert.That(clip.TryGetLocation(point, out location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(2f));
        }

        [Test]
        public void BezierLookupUsesTheCurrentSampledShapeAndRecoversAfterEditing()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var handle = new Planar.ControlHandle(new Vector2(5f, 10f));
            using var curve = new Planar.PolySegment(from, to, new Planar.QuadraticBezierShaper());
            curve.Handles.Add(handle);
            curve.Values[ShaperValues.TargetSegmentLength] = 100f;
            Assert.That(curve.TryGetLocation(new Vector2(5f, 0f), out var location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(5f));
            Assert.That(curve.TryGetLocation(new Vector2(5f, 5f), out _), Is.False);
            curve.Values[ShaperValues.TargetSegmentLength] = 1f;
            Assert.That(curve.TryGetLocation(new Vector2(5f, 5f), out location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(curve.Length / 2f).Within(0.00001f));
            Assert.That(curve.TryGetPosition(location, out var point), Is.True);
            Assert.That(Vector2.Distance(point, new Vector2(5f, 5f)), Is.LessThan(0.00001f));
            curve.Shaper = new Planar.PolylineShaper();
            Assert.That(curve.TryGetLocation(new Vector2(5f, 10f), out _), Is.True);
            curve.DisconnectNode(to);
            Assert.That(curve.TryGetLocation(Vector2.Zero, out _), Is.False);
            curve.ConnectNode(ConnectionSide.To, to);
            Assert.That(curve.TryGetLocation(Vector2.Zero, out _), Is.True);
            handle.Dispose();
            Assert.That(curve.TryGetLocation(Vector2.Zero, out _), Is.False);
        }

        [TestCase(3)]
        [TestCase(7)]
        [TestCase(11)]
        public void RoundedCompositeLengthsStillReachAnExactTerminalEndpoint(int repetitions)
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(0.3f, 0f));
            using var line = new Planar.LinearSegment(from, to);
            using var inner = new Route<Vector2, float>();
            using var outer = new Route<Vector2, float>();
            for (int i = 0; i < repetitions; i++) inner.Append(new SegmentView(line, i % 2 == 0));
            outer.Append(new SegmentView(inner, false));
            foreach (IReadOnlySegment<Vector2> query in new IReadOnlySegment<Vector2>[] { inner, outer })
            {
                Assert.That(query.TryGetPosition(new Location(query.Length), out var point, epsilon: 0f), Is.True);
                Assert.That(query.TryGetLocation(point, query.Length, query.Length,
                    out var location, out var error, epsilon: 0f), Is.True);
                Assert.That(location.Distance, Is.EqualTo(query.Length));
                Assert.That(error, Is.Zero);
            }
        }

        [Test]
        public void ScalarImplementationsComposeWithoutPlanarOrRotationArithmetic()
        {
            using var from = new Node<float>(10f);
            using var to = new Node<float>(0f);
            using var line = new GenericRouteTests.ScalarSegment(from, to);
            using var start = new SegmentPin<float, bool>(line, new Location(8f));
            using var end = new SegmentPin<float, bool>(line, new Location(2f));
            using var clip = new Clip<float, bool>(start, end);
            using var route = new Route<float, bool>();
            route.Append(new SegmentView(clip, false));
            Assert.That(route.TryGetLocation(7f, out var location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(1f));
            Assert.That(route.TryGetLocation(7f, 6f, 0f, out location, out var error,
                false, DistanceType.Rate), Is.True);
            Assert.That(location.Distance, Is.EqualTo(5f / 6f));
            Assert.That(error, Is.Zero);
        }

        [Test]
        public void QueriesAreReadOnlyAndFollowNodeHandlePinAndRouteEdits()
        {
            using var scene = new OverlapScene();
            using var start = new SegmentPin<Vector2, float>(scene.Segment, new Location(12f));
            using var end = new SegmentPin<Vector2, float>(scene.Segment, new Location(28f));
            using var clip = new Clip<Vector2, float>(start, end);
            using var route = new Route<Vector2, float>();
            route.Append(new SegmentView(clip));
            var updates = 0;
            var owner = new object();
            scene.Segment.EventHub.Connect(owner).OnUpdated(_ => updates++);
            clip.EventHub.Connect(owner).OnUpdated(_ => updates++);
            route.EventHub.Connect(owner).OnUpdated(_ => updates++);
            Assert.That(route.TryGetLocation(new Vector2(3f, 0f), out var location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(5f));
            Assert.That(updates, Is.Zero);
            start.Location = new Location(22f);
            var before = updates;
            Assert.That(route.TryGetLocation(new Vector2(3f, 0f), out location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(1f));
            Assert.That(updates, Is.EqualTo(before));
            route.Clear();
            Assert.That(route.TryGetLocation(new Vector2(3f, 0f), out _), Is.False);
            scene.First.Position = new Vector2(12f, 0f);
            Assert.That(scene.Segment.TryGetLocation(new Vector2(11f, 0f), out location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(11f));
            scene.From.Position = new Vector2(-2f, 0f);
            Assert.That(scene.Segment.TryGetLocation(Vector2.Zero, out location), Is.True);
            Assert.That(location.Distance, Is.EqualTo(2f));
        }

        [Test]
        public void EmptyInvalidAndDisposedCompositeQueriesPreserveFailureOutputs()
        {
            using var clip = new Clip<Vector2, float>();
            using var route = new Route<Vector2, float>();
            foreach (IReadOnlySegment<Vector2> segment in new IReadOnlySegment<Vector2>[] { clip, route })
            {
                Assert.That(segment.TryGetLocation(Vector2.Zero, 0f, 0f, out var location, out var error), Is.False);
                Assert.That(location, Is.EqualTo(default(Location)));
                Assert.That(error, Is.Zero);
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    segment.TryGetLocation(Vector2.Zero, out _, epsilon: -1f));
            }
            clip.Dispose();
            route.Dispose();
            Assert.Throws<ObjectDisposedException>(() => clip.TryGetLocation(Vector2.Zero, out _));
            Assert.Throws<ObjectDisposedException>(() => route.TryGetLocation(Vector2.Zero, out _));
        }

        private class OverlapScene : IDisposable
        {
            public Planar.Node From { get; } = new(Vector2.Zero);
            public Planar.Node To { get; } = new(new Vector2(10f, 0f));
            public Planar.ControlHandle First { get; } = new(new Vector2(10f, 0f));
            public Planar.ControlHandle Second { get; } = new(Vector2.Zero);
            public Planar.PolySegment Segment { get; }


            public OverlapScene()
            {
                Segment = new Planar.PolySegment(From, To);
                Segment.Values[ShaperValues.TargetSegmentLength] = 100f;
                Segment.Handles.Add(First);
                Segment.Handles.Add(Second);
            }

            public void Dispose()
            {
                Segment.Dispose();
                First.Dispose();
                Second.Dispose();
                From.Dispose();
                To.Dispose();
            }
        }
    }
}
