using System;
using System.Collections.Generic;
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
    public class GenericGeometryTests
    {
        [Test]
        public void GenericConnectionsExposeTheSameObjectsAcrossAllContracts()
        {
            using var from = new Node<float>(2f);
            using var to = new Node<float>(12f);
            using var line = new ScalarSegment();
            INode<float> node = from;
            ILinearSegment<float, bool> segment = line;
            node.ConnectSegment(segment, ConnectionSide.From);
            segment.ConnectNode(ConnectionSide.To, to);

            Assert.That(segment.From, Is.SameAs(from));
            Assert.That(((IReadOnlySegment<float>)segment).To, Is.SameAs(to));
            Assert.That(((ISegment)segment).From, Is.SameAs(from));
            Assert.That(((IReadOnlySegment)segment).To, Is.SameAs(to));
            Assert.That(((INavSegment)segment).From, Is.SameAs(from));
            Assert.That(((IReadOnlyNavSegment)segment).To, Is.SameAs(to));
            Assert.That(((IReadOnlySegment<float>)segment).Nodes, Is.EqualTo(new[] { from, to }));
            Assert.That(node.Segments.Single(), Is.SameAs(segment));
            Assert.That(((IReadOnlyNode<float>)node).Segments.Single(), Is.SameAs(segment));
            Assert.That(((INode)node).Segments.Single(), Is.SameAs(segment));
            Assert.That(((IReadOnlyNode)node).Segments.Single(), Is.SameAs(segment));
            Assert.That(((INavNode)node).Segments.Single(), Is.SameAs(segment));
            Assert.That(((IReadOnlyNavNode)node).Segments.Single(), Is.SameAs(segment));

            var success = segment.TryGetTransform(
                new Location(0.25f, DistanceType.Rate), out var position, out var forward);
            Assert.That(success, Is.True);
            Assert.That(position, Is.EqualTo(4.5f));
            Assert.That(forward, Is.True);
            Assert.That(segment.TryGetPosition(new Location(2f, isForward: false), out position), Is.True);
            Assert.That(position, Is.EqualTo(10f));

            var updates = 0;
            from.EventHub.Connect(new object()).OnUpdatedWhere(
                events => (events & (int)GeoEvents.Shape) != 0, () => updates++);
            from.Position = 4f;
            from.Position = 4f;
            Assert.That(updates, Is.EqualTo(1));
            Assert.That(((IPositioned<float>)from).Position, Is.EqualTo(4f));
            Assert.That(segment.TryGetPosition(new Location(0.5f, DistanceType.Rate), out position), Is.True);
            Assert.That(position, Is.EqualTo(8f));

            node.DisconnectSegment(segment);
            Assert.That(segment.From, Is.Null);
            Assert.That(node.Segments, Is.Empty);
            to.Dispose();
            Assert.That(segment.Nodes, Is.Empty);
            Assert.That(segment.IsDisposed, Is.False);
        }

        [Test]
        public void PositionOnlyImplementationDoesNotRequireRotationOrSegmentBaseClass()
        {
            using var from = new Node<string>("start");
            using var to = new Node<string>("end");
            using var segment = new NamedSegment();
            segment.ConnectNode(ConnectionSide.From, from);
            segment.ConnectNode(ConnectionSide.To, to);
            IReadOnlySegment<string> query = segment;
            Assert.That(query.TryGetPosition(new Location(0f), out var position), Is.True);
            Assert.That(position, Is.EqualTo("start"));
            Assert.That(query.TryGetPosition(new Location(1f), out position), Is.True);
            Assert.That(position, Is.EqualTo("end"));
            Assert.That(query.TryGetLocation("start", out var location, false, DistanceType.Rate), Is.True);
            Assert.That(location, Is.EqualTo(new Location(1f, DistanceType.Rate, false)));
            Assert.That(query.TryGetLocation("end", 0.25f, 0.75f, out location, out var error), Is.True);
            Assert.That(location.Distance, Is.EqualTo(0.25f));
            Assert.That(error, Is.Zero);
            Assert.That(query.TryGetLocation("end", 0.75f, 0.25f, out location, out error), Is.True);
            Assert.That(location.Distance, Is.EqualTo(0.75f));
            Assert.That(query.TryGetLocation("missing", out location), Is.False);
            Assert.That(location, Is.EqualTo(default(Location)));
            Assert.That(((IReadOnlyNode<string>)from).Segments.Single(), Is.SameAs(segment));
        }

        [Test]
        public void IncompatiblePositionTypesAreRejectedBeforeEitherConnectionChanges()
        {
            using var node = new Node<float>(1f);
            using var other = new Node<string>("other");
            using var raw = new NavSegment();
            using var segment = new ScalarSegment();
            Assert.Throws<ArgumentException>(() => segment.ConnectNode(ConnectionSide.From, other));
            Assert.Throws<ArgumentException>(() => raw.ConnectNode(ConnectionSide.From, node));
            Assert.That(other.Segments, Is.Empty);
            Assert.That(node.Segments, Is.Empty);
            Assert.That(segment.IsIsolated && raw.IsIsolated, Is.True);
            node.Dispose();
            Assert.Throws<ObjectDisposedException>(() => node.Position = 2f);
        }

        [Test]
        public void GenericQueriesPreserveFailureOutputsToleranceAndLifetime()
        {
            using var from = new Node<float>(0f);
            using var to = new Node<float>(10f);
            using var segment = new ScalarSegment();
            segment.ConnectNode(ConnectionSide.From, from);
            segment.ConnectNode(ConnectionSide.To, to);
            Assert.That(segment.TryGetTransform(new Location(11f), out var position, out var rotation), Is.False);
            Assert.That(position, Is.Zero);
            Assert.That(rotation, Is.False);
            Assert.That(segment.TryGetPosition(new Location(-1f), out position), Is.False);
            Assert.That(position, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => segment.Epsilon = -1f);
            segment.Epsilon = 0.1f;
            Assert.That(segment.TryGetPosition(new Location(-0.05f), out position), Is.True);
            Assert.That(position, Is.Zero);
            Assert.That(segment.TryGetPosition(new Location(-0.05f), out _, epsilon: 0f), Is.False);
            segment.Dispose();
            Assert.Throws<ObjectDisposedException>(() => segment.TryGetPosition(default, out _));
            Assert.That(from.Segments, Is.Empty);
        }

        [Test]
        public void GenericConsumersObservePlanarPinsClipsAndRoutesAfterSourceUpdates()
        {
            using var from = new Planar.Node(Vector2.Zero);
            using var to = new Planar.Node(new Vector2(10f, 0f));
            using var line = new Planar.LinearSegment(from, to);
            using var start = new SegmentPin(line, new Location(0.2f, DistanceType.Rate));
            using var end = new SegmentPin(line, new Location(0.8f, DistanceType.Rate));
            using var clip = new Clip(start, end);
            using var route = new Route();
            route.Append(new SegmentView(clip));

            ILinearSegment<Vector2, float> linear = line;
            INode<Vector2> pin = start;
            IReadOnlySegment<Vector2, float> query = route;
            Assert.That(linear.From, Is.SameAs(from));
            Assert.That(pin.Segments.Single(), Is.SameAs(clip));
            Assert.That(((IReadOnlyNode<Vector2>)from).Segments.Single(), Is.SameAs(line));
            Assert.That(((ISegment<Vector2>)route).From, Is.SameAs(start));
            Assert.That(query.Nodes, Is.EqualTo(new[] { start, end }));
            var success = query.TryGetTransform(new Location(1f, isForward: false), out var position, out var rotation);
            Assert.That(success, Is.True);
            Assert.That(position, Is.EqualTo(new Vector2(7f, 0f)));
            Assert.That(rotation, Is.EqualTo(180f));

            to.Position = new Vector2(0f, 20f);
            Assert.That(pin.Position, Is.EqualTo(new Vector2(0f, 4f)));
            Assert.That(query.TryGetTransform(new Location(1f), out position, out rotation), Is.True);
            Assert.That(position, Is.EqualTo(new Vector2(0f, 5f)));
            Assert.That(rotation, Is.EqualTo(90f));
            Assert.That(query.TryGetPosition(new Location(1f), out var direct), Is.True);
            Assert.That(direct, Is.EqualTo(position));

            route.Clear();
            Assert.That(query.TryGetTransform(default, out position, out rotation), Is.False);
            Assert.That(position, Is.EqualTo(Vector2.Zero));
            Assert.That(rotation, Is.Zero);
        }

        private class ScalarSegment : Segment<float, bool>, ILinearSegment<float, bool>
        {
            protected override int OnNodeConnected(INavNode node)
            {
                node.EventHub.Connect(this).OnUpdatedWhere(
                    events => (events & (int)GeoEvents.Shape) != 0, UpdateLength);
                UpdateLength();
                return (int)GeoEvents.Shape;
            }

            protected override int OnNodeDisconnected(INavNode node)
            {
                node.EventHub.Disconnect(this);
                UpdateLength();
                return (int)GeoEvents.Shape;
            }

            private void UpdateLength() =>
                Length = From == null || To == null ? 0f : Math.Abs(To.Position - From.Position);


            public override bool TryGetLocation(
                float position,
                float startDistance,
                float endDistance,
                out Location location,
                out double errorSquared,
                bool isForward = true,
                DistanceType distanceType = DistanceType.Length,
                float? epsilon = null)
            {
                ThrowIfDisposed();
                var query = new LocationQuery(Length, startDistance, endDistance,
                    isForward, distanceType, epsilon ?? Epsilon);
                if (query.HasValidLength && From != null && To != null && Location.IsFinite(position))
                {
                    var direction = To.Position >= From.Position ? 1 : -1;
                    var distance = Math.Clamp(((double)position - From.Position) * direction,
                        query.MinDistance, query.MaxDistance);
                    var error = position - (From.Position + direction * distance);
                    query.Consider(distance, error * error);
                }
                return query.TryGetResult(out location, out errorSquared);
            }

            public override bool TryGetTransform(
                Location location,
                out float position,
                out bool rotation,
                float? epsilon = null)
            {
                ThrowIfDisposed();
                var tolerance = epsilon ?? Epsilon;
                Location.ValidateEpsilon(tolerance);
                position = default;
                rotation = default;
                if (From == null || To == null || Length <= tolerance
                    || !location.TryResolve(Length, tolerance, out var distance)) return false;
                position = From.Position + (To.Position - From.Position) * distance / Length;
                rotation = location.IsForward;
                return true;
            }
        }

        private class NamedSegment : NavSegment, ISegment<string>
        {
            public new INode<string> From => (INode<string>)base.From;
            public new INode<string> To => (INode<string>)base.To;
            public new IEnumerable<INode<string>> Nodes => base.Nodes.Cast<INode<string>>();
            IReadOnlyNode<string> IReadOnlySegment<string>.From => From;
            IReadOnlyNode<string> IReadOnlySegment<string>.To => To;
            IEnumerable<IReadOnlyNode<string>> IReadOnlySegment<string>.Nodes => Nodes;


            public NamedSegment() : base(1f) { }

            public bool TryGetLocation(
                string position,
                out Location location,
                bool isForward = true,
                DistanceType distanceType = DistanceType.Length,
                float? epsilon = null) =>
                TryGetLocation(position, 0f, Length, out location, out _, isForward, distanceType, epsilon);

            public bool TryGetLocation(
                string position,
                float startDistance,
                float endDistance,
                out Location location,
                out double errorSquared,
                bool isForward = true,
                DistanceType distanceType = DistanceType.Length,
                float? epsilon = null)
            {
                ThrowIfDisposed();
                var query = new LocationQuery(Length, startDistance, endDistance,
                    isForward, distanceType, epsilon ?? 0f);
                if (From != null && To != null)
                {
                    if (position == From.Position) query.Consider(0, 0);
                    if (position == To.Position)
                    {
                        var distance = query.SearchForward ? Math.Max(float.Epsilon, query.MinDistance)
                            : query.MaxDistance;
                        if (distance > 0) query.Consider(distance, 0);
                    }
                }
                return query.TryGetResult(out location, out errorSquared);
            }

            public bool TryGetPosition(Location location, out string position, float? epsilon = null)
            {
                ThrowIfDisposed();
                position = default;
                if (From == null || To == null || !location.TryResolve(Length, epsilon ?? 0f, out var distance))
                    return false;
                position = distance == 0f ? From.Position : To.Position;
                return true;
            }
        }
    }
}
