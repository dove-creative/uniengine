using System;
using System.Linq;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("GeoGroups")]
    public class GenericRouteTests
    {
        internal class ScalarSegment : LinearSegment<float, bool>
        {
            public ScalarSegment(INode<float> from, INode<float> to)
            {
                ConnectNode(ConnectionSide.From, from);
                ConnectNode(ConnectionSide.To, to);
            }

            protected override void UpdateShape() =>
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
                Location location, out float position, out bool rotation, float? epsilon = null)
            {
                ThrowIfDisposed();
                position = default;
                rotation = default;
                var tolerance = epsilon ?? Epsilon;
                Location.ValidateEpsilon(tolerance);
                if (Length <= tolerance || !location.TryResolve(Length, tolerance, out var distance)) return false;
                position = From.Position + (To.Position - From.Position) * distance / Length;
                rotation = location.IsForward;
                return true;
            }
        }

        [Test]
        public void PositionOnlyPinTracksANonNumericPosition()
        {
            using var node = new Node<string>("before");
            using var pin = new NodePin<string>(node);
            IPin<string> query = pin;
            Assert.That(query.Position, Is.EqualTo("before"));
            node.Position = "after";
            Assert.That(query.Position, Is.EqualTo("after"));
            Assert.That(((INodePin)pin).AnchorNode, Is.SameAs(node));
            pin.Disconnect();
            node.Position = "ignored";
            Assert.That(pin.Position, Is.EqualTo("after"));
            Assert.That(pin.IsValid, Is.False);
            pin.Connect(node);
            node.Dispose();
            Assert.That(pin.IsDisposed, Is.True);
        }

        [Test]
        public void HoldSwitchesAnchorKindsAndOwnsOnlyItsPin()
        {
            using var from = new Node<float>(0f);
            using var to = new Node<float>(10f);
            using var segment = new ScalarSegment(from, to);
            using var hold = new Hold<float, bool>();
            hold.MoveTo(from);
            var first = hold.Anchor;
            from.Position = 2f;
            Assert.That(hold.Position, Is.EqualTo(2f));
            hold.MoveTo(to);
            Assert.That(hold.Anchor, Is.SameAs(first));
            hold.MoveTo(segment, new Location(0.5f, DistanceType.Rate));
            Assert.That(first.IsDisposed, Is.True);
            Assert.That(hold.Position, Is.EqualTo(6f));
            var second = hold.Anchor;
            hold.MoveTo(segment, new Location(0.25f, DistanceType.Rate));
            Assert.That(hold.Anchor, Is.SameAs(second));
            Assert.That(hold.Position, Is.EqualTo(4f));
            hold.Dispose();
            Assert.That(second.IsDisposed, Is.True);
            Assert.That(segment.IsDisposed || from.IsDisposed || to.IsDisposed, Is.False);
        }

        [Test]
        public void ClipAndNestedRoutesComposeGenericLocationAndRotation()
        {
            using var from = new Node<float>(0f);
            using var to = new Node<float>(10f);
            using var segment = new ScalarSegment(from, to);
            using var start = new SegmentPin<float, bool>(segment, new Location(2f));
            using var end = new SegmentPin<float, bool>(segment, new Location(8f));
            using var clip = new Clip<float, bool>(start, end);
            using var inner = new Route<float, bool>();
            using var outer = new Route<float, bool>();
            inner.Append(new SegmentView(clip, false));
            outer.Append(new SegmentView(inner, false));

            Assert.That(clip.Length, Is.EqualTo(6f));
            Assert.That(outer.TryGetTransform(new Location(1f), out var position, out var rotation), Is.True);
            Assert.That(position, Is.EqualTo(3f));
            Assert.That(rotation, Is.True);
            Assert.That(outer.TryGetTransform(new Location(1f, isForward: false), out position, out rotation), Is.True);
            Assert.That(position, Is.EqualTo(7f));
            Assert.That(rotation, Is.False);
            Assert.That(outer.TryGetTerminalSegment(new Location(1f), out var terminal, out var local), Is.True);
            Assert.That(terminal, Is.SameAs(segment));
            Assert.That(local.Distance, Is.EqualTo(3f));
            Assert.That(((IClip)clip).From, Is.SameAs(start));
            Assert.That(((ISegment<float, bool>)clip).From, Is.SameAs(start));

            end.Location = new Location(6f);
            Assert.That(outer.Length, Is.EqualTo(4f));
            Assert.That(outer.TryGetPosition(new Location(4f), out position), Is.True);
            Assert.That(position, Is.EqualTo(6f));
            Assert.That(outer.TryGetTransform(new Location(5f), out position, out rotation), Is.False);
            Assert.That(position, Is.Zero);
            Assert.That(rotation, Is.False);
        }

        [Test]
        public void GenericRouteFindsPathsWithNavigationAdapterAndTypedResults()
        {
            using var from = new Node<float>(0f);
            using var middle = new Node<float>(4f);
            using var to = new Node<float>(10f);
            using var first = new ScalarSegment(from, middle);
            using var second = new ScalarSegment(middle, to);
            using var route = new Route<float, bool>(new PathfinderAdapter<float>(new BfsPathfinder()));
            route.ConnectNode(ConnectionSide.From, from);
            route.ConnectNode(ConnectionSide.To, to);
            Assert.That(route.Views.Select(view => view.Source), Is.EqualTo(new[] { first, second }));
            Assert.That(route.Nodes, Is.EqualTo(new[] { from, middle, to }));
            Assert.That(((IReadOnlySegment<float>)route).From, Is.SameAs(from));
            Assert.That(((IReadOnlyNavSegment)route).To, Is.SameAs(to));
            Assert.That(route.TryGetTransform(new Location(7f), out var position, out var rotation), Is.True);
            Assert.That(position, Is.EqualTo(7f));
            Assert.That(rotation, Is.True);
            Assert.That(from.Segments, Does.Not.Contain(route));
        }

        [Test]
        public void GenericTypeErrorsAndCyclesPreserveExistingReferences()
        {
            using var from = new Node<float>(0f);
            using var to = new Node<float>(10f);
            using var wrong = new Node<string>("wrong");
            using var segment = new ScalarSegment(from, to);
            using var pin = new NodePin<float>(from);
            using var route = new Route<float, bool>();
            using var wrongRotation = new Route<float, int>();
            route.Append(new SegmentView(segment));
            Assert.Throws<ArgumentException>(() => ((INodePin)pin).Connect(wrong));
            Assert.That(pin.AnchorNode, Is.SameAs(from));
            Assert.Throws<ArgumentException>(() => route.ConnectNode(ConnectionSide.From, wrong));
            Assert.Throws<ArgumentException>(() => route.Append(new SegmentView(wrongRotation)));
            Assert.Throws<ArgumentException>(() => route.Append(new SegmentView(route)));
            Assert.That(route.Views.Single().Source, Is.SameAs(segment));
            using var secondPin = new NodePin<float>(pin);
            Assert.Throws<ArgumentException>(() => pin.Connect(secondPin));
            Assert.That(pin.AnchorNode, Is.SameAs(from));
        }

        [Test]
        public void GenericClipToleranceIsUsedByTerminalResolution()
        {
            using var from = new Node<float>(0f);
            using var to = new Node<float>(10f);
            using var segment = new ScalarSegment(from, to);
            using var start = new SegmentPin<float, bool>(segment, new Location(2f));
            using var end = new SegmentPin<float, bool>(segment, new Location(8f));
            using var clip = new Clip<float, bool>(start, end) { Epsilon = 0.1f };
            Assert.That(TerminalPath.For(clip).TryResolve(
                new Location(-0.05f), -1, out var terminal, out var local, out _), Is.True);
            Assert.That(terminal, Is.SameAs(segment));
            Assert.That(local.Distance, Is.EqualTo(2f));
        }

        [Test]
        public void OriginalDisposalPropagatesThroughGenericReferences()
        {
            using var from = new Node<float>(0f);
            using var to = new Node<float>(10f);
            using var segment = new ScalarSegment(from, to);
            using var hold = new Hold<float, bool>();
            using var start = new SegmentPin<float, bool>(segment, new Location(2f));
            using var end = new SegmentPin<float, bool>(segment, new Location(8f));
            using var clip = new Clip<float, bool>(start, end);
            using var route = new Route<float, bool>();
            hold.MoveTo(segment, new Location(4f));
            route.Append(new SegmentView(clip));
            segment.Dispose();
            Assert.That(new IPin[] { hold, start, end }.All(pin => pin.IsDisposed), Is.True);
            Assert.That(clip.IsDisposed && route.IsDisposed, Is.True);
            Assert.That(from.IsDisposed || to.IsDisposed, Is.False);
        }
    }
}
