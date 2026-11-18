using System;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem;
using BlackThunder.GeoSystem.RouteSystem.Planar;
using NUnit.Framework;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;
using Route = BlackThunder.GeoSystem.RouteSystem.Planar.Route;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("Tracer")]
    public class TracerTests
    {
        private static Planar.Node Node(float x, float y = 0f) => new(new Vector2(x, y));

        private class PositionOnlySegment : Planar.LinearSegment, IReadOnlySegment<Vector2, float>
        {
            public PositionOnlySegment(Planar.Node from, Planar.Node to) : base(from, to) { }

            bool IReadOnlySegment<Vector2, float>.TryGetTransform(Location location,
                out Vector2 position, out float rotation, float? epsilon)
            {
                position = default;
                rotation = default;
                return false;
            }
        }

        private static void CheckMove(ITracer<Vector2, float> tracer, float distance, bool success,
            TracerState state, float remaining)
        {
            Assert.That(tracer.Move(distance, out var actualState, out var actualRemaining), Is.EqualTo(success));
            Assert.That(actualState, Is.EqualTo(state));
            Assert.That(tracer.State, Is.EqualTo(state));
            Assert.That(actualRemaining, Is.EqualTo(remaining).Within(0.00001f));
        }

        private static void CheckPosition(ITracer<Vector2, float> tracer, float x, float y = 0f)
        {
            Assert.That(tracer.TryGetTransform(out var position, out _), Is.True);
            Assert.That(Vector2.Distance(position, new Vector2(x, y)), Is.LessThan(0.00001f));
        }

        [Test]
        public void LocationSourceControlsResizeRouteLossAndReversal()
        {
            using var start = Node(0f);
            using var joint = Node(4f);
            using var end = Node(10f);
            using var b = new Planar.LinearSegment(start, joint);
            using var a = new Planar.LinearSegment(joint, end);
            using var route = new Route();
            route.Append(new SegmentView(b));
            route.Append(new SegmentView(a));
            var relative = new Tracer<Vector2, float>();
            var absolute = new Tracer<Vector2, float>();
            Assert.That(relative.PlaceOnRoute(route, new Location(6f)), Is.True);
            Assert.That(absolute.PlaceOnRoute(route, new Location(6f), LocationType.Absolute), Is.True);
            start.Position = new Vector2(-1, 0);
            CheckPosition(relative, 5f);
            CheckPosition(absolute, 6f);
            CheckMove(relative, 8f, true, TracerState.Tracking, 3f);
            CheckPosition(relative, 10f);
            route.Dispose();
            CheckMove(relative, 2f, false, TracerState.Derailed, 2f);
            Assert.That(absolute.Wheel.MoveFailureOption, Is.EqualTo(MoveFailureOption.Derailed));
            Assert.That((int)MoveFailureOption.Derailed, Is.Zero);
            absolute.Wheel.MoveFailureOption = MoveFailureOption.TrackingFailure;
            CheckMove(absolute, 2f, false, TracerState.Rolling, 2f);
            CheckPosition(absolute, 6f);
            absolute.Wheel.MoveFailureOption = MoveFailureOption.Derailed;
            CheckMove(absolute, 8f, true, TracerState.Rolling, 4f);
            absolute.IsForward = false;
            CheckMove(absolute, 8f, true, TracerState.Rolling, 0f);
            CheckPosition(absolute, 2f);
            b.Dispose();
            CheckMove(absolute, 1f, false, TracerState.Derailed, 1f);
        }

        [TestCase(1, 2, 0)]
        [TestCase(-1, 2, 0)]
        [TestCase(20, 2, 0)]
        [TestCase(0, 1, 1)]
        public void ReservationsUseFilteredOrderAndActualCrossings(int reservation, int x, int y)
        {
            using var start = Node(0f);
            using var joint = Node(1f);
            using var lead = new Planar.LinearSegment(start, joint);
            using var blocked = new Planar.LinearSegment(joint, Node(1f, -2f));
            blocked.TraversalOptions = TraversalOptions.Backward;
            using var turn = new Planar.LinearSegment(joint, Node(1f, 3f));
            using var straight = new Planar.LinearSegment(Node(5f), joint);
            var tracer = new Tracer<Vector2, float>();
            tracer.PlaceOnSegment(lead);
            tracer.Wheel.ReservedRoutes.Enqueue(reservation);
            tracer.Wheel.ReservedRoutes.Enqueue(0);
            CheckMove(tracer, 1f, true, TracerState.Rolling, 0f);
            Assert.That(tracer.Wheel.ReservedRoutes.Count, Is.EqualTo(2));
            CheckMove(tracer, 1f, true, TracerState.Rolling, 0f);
            Assert.That(tracer.Wheel.ReservedRoutes.ToArray(), Is.EqualTo(new[] { 0 }));
            CheckPosition(tracer, x, y);
        }

        [Test]
        public void OneMoveConsumesTwoReservationsButSkipsSingleExits()
        {
            using var joint = Node(1f);
            using var single = Node(2f);
            using var fork = Node(3f);
            using var first = new Planar.LinearSegment(Node(0f), joint);
            using var spare = new Planar.LinearSegment(joint, Node(1f, 5f));
            using var middle = new Planar.LinearSegment(joint, single);
            using var next = new Planar.LinearSegment(single, fork);
            using var turn = new Planar.LinearSegment(fork, Node(3f, 2f));
            using var straight = new Planar.LinearSegment(fork, Node(6f));
            var tracer = new Tracer<Vector2, float>();
            tracer.PlaceOnSegment(first);
            tracer.Wheel.ReservedRoutes.Enqueue(1);
            tracer.Wheel.ReservedRoutes.Enqueue(0);
            CheckMove(tracer, 4f, true, TracerState.Rolling, 0f);
            Assert.That(tracer.Segment, Is.SameAs(turn));
            Assert.That(tracer.Wheel.ReservedRoutes, Is.Empty);
            CheckPosition(tracer, 3f, 1f);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void EqualOrUnmeasurableAnglesChooseFirstCandidate(bool measurable)
        {
            using var joint = Node(1f);
            using var first = new Planar.LinearSegment(Node(0f), joint);
            using var a = new Planar.LinearSegment(joint, Node(2f, 1f));
            using var b = new Planar.LinearSegment(joint, Node(2f, -1f));
            var tracer = new Tracer<Vector2, float>(measurable ? null : (_, _) => float.NaN);
            tracer.PlaceOnSegment(first);
            CheckMove(tracer, 1.5f, true, TracerState.Rolling, 0f);
            Assert.That(tracer.Segment, Is.SameAs(a));
        }

        [Test]
        public void RollingCanLeaveASegmentWithNoHeading()
        {
            using var joint = Node(2f);
            using var incoming = new PositionOnlySegment(Node(0f), joint);
            using var first = new Planar.LinearSegment(joint, Node(2f, 2f));
            using var straight = new Planar.LinearSegment(joint, Node(4f));
            var tracer = new Tracer<Vector2, float>();
            Assert.That(tracer.PlaceOnSegment(incoming), Is.True);
            Assert.That(tracer.TryGetTransform(out _, out _), Is.False);
            CheckMove(tracer, 3f, true, TracerState.Rolling, 0f);
            Assert.That(tracer.Segment, Is.SameAs(first));
            CheckPosition(tracer, 2f, 1f);
        }

        [Test]
        public void RollingUsesClipEndpointsAndExposesTheUnderlyingSegment()
        {
            using var source = new Planar.LinearSegment(Node(0f), Node(10f));
            using var from = new SegmentPin(source, new Location(8f));
            using var to = new SegmentPin(source, new Location(2f));
            using var clip = new Clip(from, to);
            using var lead = new Planar.LinearSegment(Node(10f), from);
            using var exit = new Planar.LinearSegment(to, Node(2f, 4f));
            var tracer = new Tracer<Vector2, float>();
            tracer.PlaceOnSegment(lead);
            CheckMove(tracer, 4f, true, TracerState.Rolling, 0f);
            Assert.That(tracer.Segment, Is.SameAs(source));
            Assert.That(tracer.Route, Is.Null);
            CheckPosition(tracer, 6f);
            CheckMove(tracer, 6f, true, TracerState.Rolling, 0f);
            Assert.That(tracer.Segment, Is.SameAs(exit));
            CheckPosition(tracer, 2f, 2f);
        }

        [Test]
        public void DetachRouteIsLocalToTheTracer()
        {
            using var edge = new Planar.LinearSegment(Node(0f), Node(8f));
            using var route = new Route();
            route.Append(new SegmentView(edge, false));
            var absolute = new Tracer<Vector2, float>();
            var relative = new Tracer<Vector2, float>();
            absolute.PlaceOnRoute(route, new Location(2f), LocationType.Absolute);
            relative.PlaceOnRoute(route, new Location(2f));
            absolute.DetachRoute();
            Assert.That(absolute.Route, Is.Null);
            CheckMove(absolute, 1f, true, TracerState.Rolling, 0f);
            CheckPosition(absolute, 5f);
            CheckMove(relative, 1f, true, TracerState.Tracking, 0f);
            relative.DetachRoute();
            CheckMove(relative, 1f, false, TracerState.Derailed, 1f);
            Assert.That(route.Views.Count, Is.EqualTo(1));
        }

        [TestCase(24f, 0f, 0f)]
        [TestCase(1073741824f, 3f, 1f)]
        public void RollingOnACycleConsumesWholeLaps(float distance, float x, float y)
        {
            using var origin = Node(0f);
            using var east = Node(3f);
            using var top = Node(3f, 4f);
            using var first = new Planar.LinearSegment(origin, east);
            using var second = new Planar.LinearSegment(east, top);
            using var third = new Planar.LinearSegment(top, origin);
            var tracer = new Tracer<Vector2, float>();
            tracer.PlaceOnSegment(first);
            CheckMove(tracer, distance, true, TracerState.Rolling, 0f);
            CheckPosition(tracer, x, y);
            Assert.That(tracer.Segment, Is.SameAs(distance == 24f ? third : second));
        }

        [Test]
        public void AbsoluteTracksOccurrenceAcrossPrependAndRemoval()
        {
            using var edge = new Planar.LinearSegment(Node(0f), Node(5f));
            using var route = new Route();
            route.Append(new SegmentView(edge));
            route.Append(new SegmentView(edge, false));
            var tracer = new Tracer<Vector2, float>();
            tracer.PlaceOnRoute(route, new Location(6f), LocationType.Absolute);
            route.Append(new SegmentView(edge), false);
            route.Remove(edge);
            route.Remove(edge);
            CheckMove(tracer, 1f, true, TracerState.Tracking, 0f);
            CheckPosition(tracer, 3f);
            route.Clear();
            route.Append(new SegmentView(edge, false));
            CheckMove(tracer, 1f, true, TracerState.Rolling, 0f);
            CheckPosition(tracer, 2f);
        }

        [Test]
        public void NestedClippedJoinUsesTravelDirectionWithEveryHint()
        {
            using var joint = Node(4f);
            using var a = new Planar.LinearSegment(Node(0f), joint);
            using var b = new Planar.LinearSegment(joint, Node(4f, 6f));
            using var inner = new Route();
            inner.Append(new SegmentView(a));
            inner.Append(new SegmentView(b));
            using var from = new SegmentPin(inner, new Location(8f));
            using var to = new SegmentPin(inner, new Location(2f));
            using var clip = new Clip(from, to);
            using var outer = new Route();
            outer.Append(new SegmentView(clip, false));
            foreach (var hint in new[] { -1, 0, 1, int.MaxValue })
            foreach (var forward in new[] { true, false })
            {
                var atJoin = new Location(forward ? 2f : 4f, isForward: forward);
                Assert.That(outer.TryGetTerminalSegment(
                    atJoin, hint, out var segment, out var local, out var index), Is.True);
                Assert.That(segment, Is.SameAs(forward ? a : b));
                Assert.That(index, Is.EqualTo(forward ? 0 : 1));
                Assert.That(local, Is.EqualTo(new Location(forward ? 4f : 6f, isForward: forward)));
            }
            from.Location = new Location(4f);
            Assert.That(outer.TryGetTerminalSegment(new Location(0f, isForward: false),
                out var atClipEnd, out _), Is.True);
            Assert.That(atClipEnd, Is.SameAs(b));
        }

        [TestCase(0.1f)]
        [TestCase(0.3f)]
        public void RoundedRouteLengthPreservesExactTerminalEndpoints(float lastLength)
        {
            using var joint = Node(0f);
            using var end = Node(0f, lastLength);
            using var first = new Planar.LinearSegment(Node(-16f), joint);
            using var last = new Planar.LinearSegment(joint, end);
            using var route = new Route();
            route.Append(new SegmentView(first));
            route.Append(new SegmentView(last));
            using var reverse = new Route();
            reverse.Append(new SegmentView(route, false));

            foreach (var forward in new[] { true, false })
            {
                var location = new Location(forward ? 1f : 0f, DistanceType.Rate, forward);
                Assert.That(route.TryGetTerminalSegment(location, out var leaf, out var local, 0f), Is.True);
                Assert.That(leaf, Is.SameAs(last));
                Assert.That(local.Distance, Is.EqualTo(forward ? last.Length : 0f));
                Assert.That(route.TryGetPosition(location, out var position, 0f), Is.True);
                Assert.That(position, Is.EqualTo(end.Position));
            }
            Assert.That(reverse.TryGetTerminalSegment(default, out var nested, out var atEnd, 0f), Is.True);
            Assert.That(nested, Is.SameAs(last));
            Assert.That(atEnd, Is.EqualTo(new Location(0f, isForward: false)));
        }

        [Test]
        public void PolyHintSurvivesReversalAndReplacementShape()
        {
            using var poly = new Planar.PolySegment(Node(0f), Node(4f, 4f));
            using var corner = new Planar.ControlHandle(new Vector2(4f, 0f));
            poly.Handles.Add(corner);
            var regular = new Tracer<Vector2, float>();
            var hinted = new PolyTracer<Vector2, float>();
            regular.PlaceOnSegment(poly);
            hinted.PlaceOnSegment(poly);
            foreach (var distance in new[] { 4f, 1f, 1f, 2f })
            {
                CheckMove(regular, distance, true, TracerState.Rolling, 0f);
                CheckMove(hinted, distance, true, TracerState.Rolling, 0f);
                regular.TryGetTransform(out var expected, out var rotation);
                Assert.That(hinted.TryGetTransform(out var actual, out var actualRotation), Is.True);
                Assert.That(actual, Is.EqualTo(expected));
                Assert.That(actualRotation, Is.EqualTo(rotation));
                regular.IsForward = hinted.IsForward = false;
                poly.Values[ShaperValues.TargetSegmentLength] = 10f;
            }
        }

        [Test]
        public void FailedPlacementAndInvalidDistancePreserveCurrentSegment()
        {
            using var edge = new Planar.LinearSegment(Node(0f), Node(4f));
            using var route = new Route();
            var tracer = new Tracer<Vector2, float>();
            Assert.That(tracer.PlaceOnSegment(edge, new Location(.5f, DistanceType.Rate)), Is.True);
            Assert.That(tracer.PlaceOnRoute(route), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => tracer.Move(float.NaN, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracer.Move(-1f, out _, out _));
            CheckPosition(tracer, 2f);
            CheckMove(tracer, 1f, true, TracerState.Rolling, 0f);
            Assert.That(tracer.Location, Is.EqualTo(new Location(.75f, DistanceType.Rate)));
        }
    }
}
