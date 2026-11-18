using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem.Planar;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("GeoSystemPlanar")]
    [Category("PolySegment")]
    public class PolySegmentTests
    {
        private class Bend : IDisposable
        {
            public Planar.Node From { get; } = new(Vector2.Zero);
            public Planar.Node To { get; } = new(new Vector2(3f, 4f));
            public Planar.ControlHandle Handle { get; } = new(new Vector2(3f, 0f));
            public Planar.PolySegment Segment { get; }


            public Bend(float target = 100f)
            {
                Segment = new Planar.PolySegment(From, To);
                Segment.Values[ShaperValues.TargetSegmentLength] = target;
                Segment.Handles.Add(Handle);
            }

            public void Dispose()
            {
                Segment.Dispose();
                Handle.Dispose();
                From.Dispose();
                To.Dispose();
            }
        }

        private class FailingShaper : IShaper<Vector2>
        {
            public bool TryShape(
                Vector2 from,
                Vector2 to,
                IReadOnlyDictionary<string, float> values,
                IReadOnlyList<Vector2> handles,
                ICollection<Vector2> positions)
            {
                positions.Add(from);
                throw new InvalidOperationException("Shape generation failed.");
            }
        }


        [Test]
        public void PublicContractsDoNotExposeHingeStorageOrConstruction()
        {
            Assert.That(typeof(Hinge<Vector2, float>).IsVisible, Is.False);
            Assert.That(typeof(IPolySegment<Vector2, float>).GetProperty("Hinges"), Is.Null);
            foreach (var type in new[] { typeof(PolySegment<Vector2, float>), typeof(Planar.PolySegment) })
            {
                Assert.That(type.GetProperty("Hinges"), Is.Null);
                var builder = type.GetMethod("TryBuildHinges", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(builder, Is.Not.Null);
                Assert.That(builder.IsFamilyAndAssembly, Is.True);
            }
        }

        [Test]
        public void PolylineShaperPreservesCornersAndSubdividesEachLeg()
        {
            using var bend = new Bend(2f);
            var segment = bend.Segment;
            Assert.That(segment.Hinges.Select(hinge => hinge.Position), Is.EqualTo(new[]
            {
                Vector2.Zero, new Vector2(1.5f, 0f), new Vector2(3f, 0f),
                new Vector2(3f, 2f), new Vector2(3f, 4f),
            }));
            Assert.That(segment.Hinges.Select(hinge => hinge.Distance), Is.EqualTo(new[] { 0f, 1.5f, 3f, 5f, 7f }));
            Assert.That(segment.Hinges.Select(hinge => hinge.Rotation), Is.EqualTo(new[] { 0f, 0f, 90f, 90f, 90f }));
            Assert.That(segment.Length, Is.EqualTo(7f));
            Assert.That(segment.Handles.Single(), Is.SameAs(bend.Handle));
            Assert.That(segment.Hinges.Last().Distance, Is.EqualTo(segment.Length));
            Assert.Throws<NotSupportedException>(() => ((IList<Hinge<Vector2, float>>)segment.Hinges).Clear());
        }

        [TestCase(true, 0f, 0f, 0f, 0f)]
        [TestCase(true, 2f, 2f, 0f, 0f)]
        [TestCase(true, 3f, 3f, 0f, 90f)]
        [TestCase(true, 5f, 3f, 2f, 90f)]
        [TestCase(true, 7f, 3f, 4f, 90f)]
        [TestCase(false, 0f, 3f, 4f, 270f)]
        [TestCase(false, 2f, 3f, 2f, 270f)]
        [TestCase(false, 4f, 3f, 0f, 180f)]
        [TestCase(false, 5f, 2f, 0f, 180f)]
        [TestCase(false, 7f, 0f, 0f, 180f)]
        public void QueriesChooseTheOutgoingLegInEachTravelDirection(
            bool forward, float distance, float x, float y, float rotation)
        {
            using var bend = new Bend();
            var point = new Vector2(x, y);
            var location = new Location(distance, isForward: forward);
            PlanarGeoSystemTests.AssertPose(bend.Segment, location, point, rotation);
            PlanarGeoSystemTests.AssertPose(bend.Segment,
                new Location(distance / 7f, DistanceType.Rate, forward), point, rotation);
            IReadOnlySegment<Vector2, float> generic = bend.Segment;
            Assert.That(generic.TryGetTransform(location, out var position, out var angle), Is.True);
            Assert.That(position, Is.EqualTo(point));
            Assert.That(angle, Is.EqualTo(rotation));
        }

        [Test]
        public void QueriesKeepBoundsToleranceFailureOutputsAndLifetimeContracts()
        {
            using var bend = new Bend();
            var segment = bend.Segment;
            foreach (var distance in new[] { -1f, 8f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.That(segment.TryGetTransform(new Location(distance), out var position, out var angle), Is.False);
                Assert.That(position, Is.EqualTo(Vector2.Zero));
                Assert.That(angle, Is.Zero);
            }
            PlanarGeoSystemTests.AssertPose(segment, new Location(-0.000005f), Vector2.Zero, 0f);
            Assert.That(segment.TryGetPosition(new Location(-0.000005f), out _, 0f), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => segment.TryGetPosition(default, out _, -1f));
            segment.Epsilon = 7f;
            Assert.That(segment.TryGetPosition(default, out _), Is.False);
            Assert.That(segment.TryGetPosition(default, out _, 0f), Is.True);
            segment.Dispose();
            Assert.Throws<ObjectDisposedException>(() => segment.TryGetPosition(default, out _));
            Assert.Throws<ObjectDisposedException>(() => segment.Values["any"] = 1f);
            Assert.Throws<ObjectDisposedException>(() => segment.Handles.Clear());
            Assert.Throws<ObjectDisposedException>(() => segment.Shaper = new PolylineShaper());
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidTargetLengthIsRejectedBeforeMutation(float value)
        {
            using var bend = new Bend();
            var updates = 0;
            bend.Segment.EventHub.Connect(new object()).OnUpdated(_ => updates++);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => bend.Segment.Values[ShaperValues.TargetSegmentLength] = value);
            Assert.That(bend.Segment.Values[ShaperValues.TargetSegmentLength], Is.EqualTo(100f));
            Assert.That(bend.Segment.Length, Is.EqualTo(7f));
            Assert.That(updates, Is.Zero);
        }

        [Test]
        public void EveryDictionaryMutationUpdatesShapeAndNoOpsStaySilent()
        {
            using var bend = new Bend();
            var segment = bend.Segment;
            var hinges = segment.Hinges;
            var updates = 0;
            segment.EventHub.Connect(new object()).OnUpdated(flags =>
            {
                Assert.That(flags, Is.EqualTo((int)GeoEvents.Shape));
                Assert.That(segment.Hinges.Last().Distance, Is.EqualTo(segment.Length));
                updates++;
            });
            segment.Values[ShaperValues.TargetSegmentLength] = 2f;
            Assert.That(hinges.Count, Is.EqualTo(5));
            segment.Values[ShaperValues.TargetSegmentLength] = 2f;
            Assert.That(segment.Values.Remove("missing"), Is.False);
            var collection = (ICollection<KeyValuePair<string, float>>)segment.Values;
            Assert.That(collection.Remove(new KeyValuePair<string, float>(ShaperValues.TargetSegmentLength, 3f)),
                Is.False);
            Assert.That(updates, Is.EqualTo(1));
            Assert.That(collection.Remove(new KeyValuePair<string, float>(ShaperValues.TargetSegmentLength, 2f)),
                Is.True);
            Assert.That(hinges.Count, Is.EqualTo(8));
            collection.Add(new KeyValuePair<string, float>(ShaperValues.TargetSegmentLength, 100f));
            Assert.That(hinges.Count, Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => segment.Values.Add(ShaperValues.TargetSegmentLength, 1f));
            segment.Values.Clear();
            segment.Values.Clear();
            Assert.That(hinges.Count, Is.EqualTo(8));
            Assert.That(updates, Is.EqualTo(4));
        }

        [Test]
        public void HandleEditsPublishCompletedStateAndKeepAbsoluteCoordinates()
        {
            using var bend = new Bend();
            var segment = bend.Segment;
            var updates = 0;
            segment.EventHub.Connect(new object()).OnUpdated(_ =>
            {
                Assert.That(segment.Hinges.First().Position, Is.EqualTo(bend.From.Position));
                Assert.That(segment.Hinges.Last().Position, Is.EqualTo(bend.To.Position));
                Assert.That(segment.Hinges.Any(hinge => hinge.Position == bend.Handle.Position), Is.True);
                updates++;
            });
            bend.Handle.Position = new Vector2(0f, 4f);
            bend.Handle.Position = new Vector2(0f, 4f);
            segment.Handles[0] = bend.Handle;
            segment.Shaper = segment.Shaper;
            Assert.That(updates, Is.EqualTo(1));
            bend.From.Position = new Vector2(-3f, 0f);
            Assert.That(bend.Handle.Position, Is.EqualTo(new Vector2(0f, 4f)));
            Assert.That(segment.Length, Is.EqualTo(8f));
            Assert.That(updates, Is.EqualTo(2));
        }

        [Test]
        public void SharedAndRepeatedHandlesUseOneSubscriptionPerSegment()
        {
            using var bend = new Bend();
            using var other = new Planar.PolySegment(bend.From, bend.To);
            bend.Segment.Handles.Add(bend.Handle);
            other.Handles.Add(bend.Handle);
            var hub = (EventHub<int>)bend.Handle.EventHub;
            Assert.That(hub.ConnectionCount, Is.EqualTo(2));
            bend.Handle.Position = new Vector2(0f, 4f);
            Assert.That(bend.Segment.Length, Is.EqualTo(7f));
            Assert.That(other.Length, Is.EqualTo(7f).Within(0.00001f));
            bend.Segment.Handles.RemoveAt(0);
            Assert.That(hub.ConnectionCount, Is.EqualTo(2));
            using var replacement = new Planar.ControlHandle(new Vector2(3f, 0f));
            bend.Segment.Handles[0] = replacement;
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            bend.Handle.Position = new Vector2(0f, 3f);
            Assert.That(bend.Segment.Hinges[1].Position, Is.EqualTo(replacement.Position));
            other.Dispose();
            Assert.That(hub.ConnectionCount, Is.Zero);
            Assert.That(bend.Handle.IsDisposed, Is.False);
            bend.Segment.Handles.Clear();
            Assert.That(((EventHub<int>)replacement.EventHub).ConnectionCount, Is.Zero);
        }

        [Test]
        public void DisposedHandleInvalidatesOnlyItsUsersAndRemovalRecovers()
        {
            using var bend = new Bend();
            bend.Handle.Dispose();
            Assert.That(bend.Segment.IsDisposed, Is.False);
            Assert.That(bend.Segment.Hinges, Is.Empty);
            Assert.That(bend.Segment.Length, Is.Zero);
            Assert.That(bend.Segment.TryGetPosition(default, out _), Is.False);
            bend.Segment.Handles.Remove(bend.Handle);
            Assert.That(bend.Segment.Length, Is.EqualTo(5f));
            Assert.Throws<ArgumentException>(() => bend.Segment.Handles.Add(bend.Handle));
            Assert.Throws<ArgumentNullException>(() => bend.Segment.Handles.Add(null));
            Assert.Throws<ObjectDisposedException>(() => bend.Handle.Position = Vector2.Zero);
        }

        [Test]
        public void ConsecutiveDuplicatesAndZeroLengthRecoverWithoutRemovingHandles()
        {
            using var from = new Planar.Node();
            using var to = new Planar.Node();
            using var handle = new Planar.ControlHandle();
            using var segment = new Planar.PolySegment(from, to);
            segment.Handles.Add(handle);
            segment.Handles.Add(handle);
            Assert.That(segment.Hinges, Is.Empty);
            handle.Position = Vector2.UnitX;
            Assert.That(segment.Hinges.Select(hinge => hinge.Position),
                Is.EqualTo(new[] { Vector2.Zero, Vector2.UnitX, Vector2.Zero }));
            Assert.That(segment.Length, Is.EqualTo(2f));
            Assert.That(segment.Handles.Count, Is.EqualTo(2));
            segment.DisconnectNode(to);
            Assert.That(segment.Hinges, Is.Empty);
            segment.ConnectNode(ConnectionSide.To, to);
            Assert.That(segment.Length, Is.EqualTo(2f));
        }

        [Test]
        public void HandleValidationRejectsNonFinitePlanarPositionsAndInvalidatesGenericInput()
        {
            using var bend = new Bend();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => bend.Handle.Position = new Vector2(float.NaN, 0f));
            Assert.That(bend.Handle.Position, Is.EqualTo(new Vector2(3f, 0f)));
            using var generic = new ControlHandle<Vector2>(Vector2.One);
            bend.Segment.Handles[0] = generic;
            generic.Position = new Vector2(float.PositiveInfinity, 0f);
            Assert.That(bend.Segment.Hinges, Is.Empty);
            generic.Position = new Vector2(3f, 0f);
            Assert.That(bend.Segment.Length, Is.EqualTo(7f));
        }

        [TestCase(2)]
        [TestCase(3)]
        public void BezierDegreeControlsInputCountAndKnownCurveSamples(int degree)
        {
            using var from = new Planar.Node();
            using var to = new Planar.Node(new Vector2(4f, 0f));
            IShaper<Vector2> shaper = degree == 2 ? new QuadraticBezierShaper() : new CubicBezierShaper();
            using var segment = new Planar.PolySegment(from, to, shaper);
            using var first = new Planar.ControlHandle(degree == 2 ? new Vector2(2f, 4f) : new Vector2(0f, 4f));
            using var second = new Planar.ControlHandle(new Vector2(4f, 4f));
            segment.Values[ShaperValues.TargetSegmentLength] = 0.125f;
            Assert.That(segment.Hinges, Is.Empty);
            segment.Handles.Add(first);
            if (degree == 3)
            {
                Assert.That(segment.Hinges, Is.Empty);
                segment.Handles.Add(second);
            }
            var middle = new Vector2(2f, degree == 2 ? 2f : 3f);
            Assert.That(segment.Hinges.Any(hinge => hinge.Position == middle), Is.True);
            Assert.That(segment.Hinges.First().Position, Is.EqualTo(from.Position));
            Assert.That(segment.Hinges.Last().Position, Is.EqualTo(to.Position));
            Assert.That(segment.Hinges.Last().Rotation, Is.EqualTo(segment.Hinges[segment.Hinges.Count - 2].Rotation));
            for (int i = 1; i < segment.Hinges.Count; i++)
                Assert.That(Vector2.Distance(segment.Hinges[i - 1].Position, segment.Hinges[i].Position),
                    Is.LessThanOrEqualTo(0.12501f));

            double referenceLength = 0;
            var previous = Vector2.Zero;
            for (int step = 1; step <= 10000; step++)
            {
                var t = step / 10000d;
                var u = 1d - t;
                var point = degree == 2
                    ? new Vector2((float)(4d * t), (float)(8d * t * u))
                    : new Vector2((float)(12d * u * t * t + 4d * t * t * t), (float)(12d * t * u));
                referenceLength += Vector2.Distance(previous, point);
                previous = point;
            }
            Assert.That(segment.Length, Is.EqualTo(referenceLength).Within(0.003d));
            Assert.That(segment.TryGetPosition(new Location(0.5f, DistanceType.Rate), out var half), Is.True);
            Assert.That(Vector2.Distance(half, middle), Is.LessThan(0.00001f));
            var count = segment.Hinges.Count;
            segment.Values[ShaperValues.TargetSegmentLength] = 0.0625f;
            Assert.That(segment.Hinges.Count, Is.GreaterThan(count));
            segment.Handles.Add(first);
            Assert.That(segment.Hinges, Is.Empty);
            segment.Handles.RemoveAt(segment.Handles.Count - 1);
            Assert.That(segment.Hinges.Count, Is.GreaterThan(count));
        }

        [Test]
        public void ShaperReplacementPreservesInputsAndCanRecoverAfterIncompleteControls()
        {
            using var bend = new Bend(0.5f);
            var values = bend.Segment.Values;
            var handles = bend.Segment.Handles;
            bend.Segment.Shaper = new CubicBezierShaper();
            Assert.That(bend.Segment.Hinges, Is.Empty);
            bend.Segment.Shaper = new QuadraticBezierShaper();
            Assert.That(bend.Segment.Hinges, Is.Not.Empty);
            bend.Segment.Shaper = new PolylineShaper();
            Assert.That(bend.Segment.Length, Is.EqualTo(7f).Within(0.00001f));
            Assert.That(bend.Segment.Values, Is.SameAs(values));
            Assert.That(bend.Segment.Handles, Is.SameAs(handles));
            Assert.That(handles.Single(), Is.SameAs(bend.Handle));
            Assert.Throws<ArgumentNullException>(() => bend.Segment.Shaper = null);
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(3)]
        public void UnrepresentableSubdivisionFailsWithoutPublishingPartialPositions(int degree)
        {
            IShaper<Vector2> shaper = degree == 0 ? new PolylineShaper()
                : degree == 2 ? new QuadraticBezierShaper() : new CubicBezierShaper();
            var values = new Dictionary<string, float> { [ShaperValues.TargetSegmentLength] = float.Epsilon };
            var handles = degree == 3 ? new[] { new Vector2(0f, 4f), new Vector2(4f, 4f) }
                : new[] { new Vector2(2f, 4f) };
            var positions = new List<Vector2> { new Vector2(-1f, -1f) };
            Assert.That(shaper.TryShape(Vector2.Zero, new Vector2(4f, 0f), values, handles, positions), Is.False);
            Assert.That(positions, Is.EqualTo(new[] { new Vector2(-1f, -1f) }));
            using var bend = new Bend();
            bend.Segment.Values[ShaperValues.TargetSegmentLength] = float.Epsilon;
            Assert.That(bend.Segment.Hinges, Is.Empty);
            bend.Segment.Values[ShaperValues.TargetSegmentLength] = 1f;
            Assert.That(bend.Segment.Length, Is.EqualTo(7f));
        }

        [Test]
        public void PinClipRouteAndPathfindingConsumeUpdatedPolyGeometry()
        {
            using var bend = new Bend();
            using var start = new SegmentPin(bend.Segment, new Location(0.25f, DistanceType.Rate));
            using var end = new SegmentPin(bend.Segment, new Location(0.75f, DistanceType.Rate));
            using var clip = new Clip(start, end);
            using var route = new Route();
            route.Append(new SegmentView(clip));
            bend.Handle.Position = new Vector2(0f, 4f);
            Assert.That(start.Position, Is.EqualTo(new Vector2(0f, 1.75f)));
            Assert.That(end.Position, Is.EqualTo(new Vector2(1.25f, 4f)));
            Assert.That(clip.Length, Is.EqualTo(3.5f));
            Assert.That(route.Length, Is.EqualTo(3.5f));
            PlanarGeoSystemTests.AssertPose(route, default, start.Position, 90f);
            bend.To.Position = new Vector2(6f, 4f);
            Assert.That(start.Position, Is.EqualTo(new Vector2(0f, 2.5f)));
            Assert.That(end.Position, Is.EqualTo(new Vector2(3.5f, 4f)));
            Assert.That(route.Length, Is.EqualTo(5f));
            using var journey = new Route();
            journey.ConnectNode(ConnectionSide.From, bend.From);
            journey.ConnectNode(ConnectionSide.To, bend.To);
            Assert.That(journey.Views.Single().Source, Is.SameAs(bend.Segment));
            Assert.That(journey.Length, Is.EqualTo(10f));
            bend.Segment.Dispose();
            Assert.That(start.IsDisposed && clip.IsDisposed && route.IsDisposed && journey.IsDisposed, Is.True);
            Assert.That(bend.Handle.IsDisposed || bend.From.IsDisposed || bend.To.IsDisposed, Is.False);
        }

        [Test]
        public void CommonAndPlanarContractsRetainConnectionsAndRejectCycles()
        {
            using var bend = new Bend();
            ISegment<Vector2, float> generic = bend.Segment;
            Assert.That(generic.From, Is.SameAs(bend.From));
            Assert.That(((IReadOnlyNavSegment)generic).To, Is.SameAs(bend.To));
            Assert.That(generic.Values, Is.SameAs(bend.Segment.Values));
            Assert.That(generic.Handles, Is.SameAs(bend.Segment.Handles));
            Assert.That(bend.From.Segments.Single(), Is.SameAs(bend.Segment));
            using var pin = new SegmentPin(bend.Segment);
            bend.Segment.DisconnectNode(bend.To);
            Assert.Throws<ArgumentException>(() => bend.Segment.ConnectNode(ConnectionSide.To, pin));
            using var foreign = new Node<Vector2>(Vector2.One);
            Assert.Throws<ArgumentException>(() => bend.Segment.ConnectNode(ConnectionSide.To, foreign));
            Assert.That(bend.Segment.To, Is.Null);
        }

        [Test]
        public void LinearClipAndRouteExposeImmutableEmptyEditingInputs()
        {
            using var bend = new Bend();
            using var line = new Planar.LinearSegment(bend.From, bend.To);
            using var start = new SegmentPin(line);
            using var end = new SegmentPin(line, new Location(1f, DistanceType.Rate));
            using var clip = new Clip(start, end);
            using var route = new Route();
            foreach (ISegment<Vector2, float> segment in new ISegment<Vector2, float>[] { line, clip, route })
            {
                Assert.That(segment.Values.Count, Is.Zero);
                Assert.That(segment.Handles.Count, Is.Zero);
                Assert.That(segment.Values.IsReadOnly && segment.Handles.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => segment.Values.Add("x", 1f));
                Assert.Throws<NotSupportedException>(() => segment.Handles.Add(bend.Handle));
            }
            Assert.That(line, Is.InstanceOf<LinearSegment<Vector2, float>>());
            Assert.That(bend.Segment, Is.InstanceOf<PolySegment<Vector2, float>>());
        }

        [Test]
        public void ObserverExceptionLeavesCompletedGeometryInPlace()
        {
            using var bend = new Bend();
            var listener = new object();
            bend.Segment.EventHub.Connect(listener).OnUpdated(_ => throw new InvalidOperationException("Observer"));
            Assert.Throws<InvalidOperationException>(() => bend.Handle.Position = new Vector2(0f, 4f));
            Assert.That(bend.Segment.Hinges[1].Position, Is.EqualTo(new Vector2(0f, 4f)));
            Assert.That(bend.Segment.Length, Is.EqualTo(7f));
            bend.Segment.EventHub.Disconnect(listener);
        }

        [Test]
        public void CustomShaperFailureInvalidatesDependentsAndReplacementRecovers()
        {
            using var bend = new Bend();
            using var pin = new SegmentPin(bend.Segment, new Location(1f));
            var updates = 0;
            bend.Segment.EventHub.Connect(new object()).OnUpdated(_ => updates++);
            Assert.Throws<InvalidOperationException>(() => bend.Segment.Shaper = new FailingShaper());
            Assert.That(bend.Segment.Hinges, Is.Empty);
            Assert.That(bend.Segment.Length, Is.Zero);
            Assert.That(pin.IsValid, Is.False);
            Assert.That(updates, Is.EqualTo(1));
            bend.Segment.Shaper = new PolylineShaper();
            Assert.That(pin.IsValid, Is.True);
            Assert.That(bend.Segment.Length, Is.EqualTo(7f));
            Assert.That(updates, Is.EqualTo(2));
        }
    }
}
