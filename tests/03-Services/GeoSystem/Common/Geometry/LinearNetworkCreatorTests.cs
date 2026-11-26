using System;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem;
using NUnit.Framework;
using PlanarNode = BlackThunder.GeoSystem.Geometry.Planar.Node;
using PlanarSegment = BlackThunder.GeoSystem.Geometry.Planar.LinearSegment;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("NetworkCreators")]
    public class LinearNetworkCreatorTests
    {
        [Test]
        public void IdleDoesNotDetectOrCreateAndUpdateOnlySelectsAState()
        {
            // Creator table: Idle and all four active leaves, without graph mutations on Update.
            using var scene = new NetworkCreatorScene();
            using var selected = new PlanarNode(Vector2.One);
            var creator = scene.Creator;
            creator.Update();
            Assert.That(creator.TryConfirm(out var result), Is.False);
            Assert.That(result.Node, Is.Null);
            Assert.That(scene.DetectionCount, Is.Zero);
            Assert.That(creator.State, Is.EqualTo(LinearNetworkCreatorState.Idle));
            creator.Begin();
            Assert.That(creator.State, Is.EqualTo(LinearNetworkCreatorState.PutFirstNode));
            creator.Begin();
            Assert.That(scene.DetectionCount, Is.EqualTo(1));
            scene.Detection = new Detection<Vector2>(Vector2.One, selected);
            creator.Update();
            Assert.That(creator.State, Is.EqualTo(LinearNetworkCreatorState.SelectFirstNode));
            Assert.That(creator.TryConfirm(out result), Is.True);
            Assert.That(result.IsNewNode, Is.False);
            Assert.That(result.Segment, Is.Null);
            Assert.That(creator.State, Is.EqualTo(LinearNetworkCreatorState.SelectNextNode));
            scene.Detection = new Detection<Vector2>(new Vector2(2, 0));
            creator.Update();
            Assert.That(creator.State, Is.EqualTo(LinearNetworkCreatorState.PutNextNode));
            Assert.That(scene.Nodes, Is.Empty);
            Assert.That(scene.Segments, Is.Empty);
        }

        [Test]
        public void ConfirmBuildsAChainAndResultsCanBeRegisteredInAGroup()
        {
            // Creator table: first placement, next placement, next selection, and group integration.
            using var scene = new NetworkCreatorScene();
            using var group = new GeoGroup<Vector2, float>();
            using var existing = new PlanarNode(new Vector2(8, 0));
            var creator = scene.Creator;
            creator.Begin();
            Assert.That(creator.TryConfirm(out var first), Is.True);
            scene.Detection = new Detection<Vector2>(new Vector2(3, 4));
            Assert.That(creator.TryConfirm(out var second), Is.True);
            scene.Detection = new Detection<Vector2>(existing.Position, existing);
            Assert.That(creator.TryConfirm(out var third), Is.True);

            foreach (var result in new[] { first, second, third })
            {
                if (result.IsNewNode) group.AddNode(result.Node);
                if (result.Segment != null) group.AddSegment((ISegment<Vector2, float>)result.Segment);
            }

            Assert.That(first.IsNewNode && second.IsNewNode, Is.True);
            Assert.That(third.IsNewNode, Is.False);
            Assert.That(second.Segment.From, Is.SameAs(first.Node));
            Assert.That(second.Segment.To, Is.SameAs(second.Node));
            Assert.That(second.Segment.Length, Is.EqualTo(5f));
            Assert.That(third.Segment.From, Is.SameAs(second.Node));
            Assert.That(third.Segment.To, Is.SameAs(existing));
            Assert.That(second.Node.Segments, Is.EquivalentTo(new[] { second.Segment, third.Segment }));
            Assert.That(group.Nodes.Count, Is.EqualTo(2));
            Assert.That(group.Segments.Count, Is.EqualTo(2));
            Assert.That(creator.AnchorNode, Is.SameAs(existing));
            creator.Finish();
            creator.Dispose();
            Assert.That(group.Segments.All(segment => !segment.IsDisposed), Is.True);
        }

        [Test]
        public void ConfirmationReadsLatestDetectionOnceAndPrefersNodeOverSegment()
        {
            // Creator table: simultaneous targets and confirmation after stale preview.
            using var scene = new NetworkCreatorScene();
            using var first = new PlanarNode(Vector2.Zero);
            using var second = new PlanarNode(Vector2.One);
            using var source = new PlanarSegment(first, second);
            scene.Creator.Begin();
            scene.Detection = new Detection<Vector2>(Vector2.One, second, source);
            var calls = scene.DetectionCount;
            Assert.That(scene.Creator.TryConfirm(out var result), Is.True);
            Assert.That(scene.DetectionCount, Is.EqualTo(calls + 1));
            Assert.That(result.Node, Is.SameAs(second));
            Assert.That(scene.Nodes, Is.Empty);
            Assert.That(scene.Creator.TryConfirm(out _), Is.False);
            Assert.That(scene.Segments, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectedOrThrowingNodeFactoryCleansReturnedNode(bool throws)
        {
            // Creator table: failed first placement leaves Anchor and cleans the returned allocation.
            using var scene = new NetworkCreatorScene { RejectNode = !throws, ThrowNode = throws };
            scene.Creator.Begin();
            if (throws) Assert.Throws<InvalidOperationException>(() => scene.Creator.TryConfirm(out _));
            else Assert.That(scene.Creator.TryConfirm(out var result), Is.False);
            Assert.That(scene.Nodes.Single().IsDisposed, Is.True);
            Assert.That(scene.Creator.AnchorNode, Is.Null);
            Assert.That(scene.Creator.State, Is.EqualTo(LinearNetworkCreatorState.PutFirstNode));
            scene.RejectNode = scene.ThrowNode = false;
            Assert.That(scene.Creator.TryConfirm(out _), Is.True);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void FailedSegmentPreservesAnchorAndSelectedNodes(bool throws, bool selectExisting)
        {
            // Creator table: failed or partially connected segments roll back only this attempt.
            using var scene = new NetworkCreatorScene();
            using var existing = new PlanarNode(new Vector2(3, 0));
            scene.Creator.Begin();
            scene.Creator.TryConfirm(out var first);
            scene.Detection = new Detection<Vector2>(existing.Position, selectExisting ? existing : null);
            scene.ThrowSegment = throws;
            scene.RejectSegment = !throws;
            if (throws) Assert.Throws<InvalidOperationException>(() => scene.Creator.TryConfirm(out _));
            else
            {
                Assert.That(scene.Creator.TryConfirm(out var result), Is.False);
                Assert.That(result.Node, Is.Null);
                Assert.That(result.Segment, Is.Null);
            }

            Assert.That(scene.Creator.AnchorNode, Is.SameAs(first.Node));
            Assert.That(first.Node.IsDisposed, Is.False);
            Assert.That(first.Node.Segments, Is.Empty);
            Assert.That(existing.IsDisposed, Is.False);
            Assert.That(existing.Segments, Is.Empty);
            Assert.That(scene.Segments.Single().IsDisposed, Is.True);
            if (!selectExisting) Assert.That(scene.Nodes.Last().IsDisposed, Is.True);
            scene.ThrowSegment = scene.RejectSegment = false;
            Assert.That(scene.Creator.TryConfirm(out _), Is.True);
        }

        [Test]
        public void CancelStepsBackAndFinishNeverDisposesConfirmedObjects()
        {
            // Creator table: Connect -> Anchor -> Idle and repeated stop/start retain committed ownership.
            using var scene = new NetworkCreatorScene();
            scene.Creator.Begin();
            scene.Creator.TryConfirm(out var first);
            scene.Creator.Cancel();
            Assert.That(scene.Creator.AnchorNode, Is.Null);
            Assert.That(scene.Creator.State, Is.EqualTo(LinearNetworkCreatorState.PutFirstNode));
            Assert.That(first.Node.IsDisposed, Is.False);
            scene.Creator.Cancel();
            Assert.That(scene.Creator.State, Is.EqualTo(LinearNetworkCreatorState.Idle));
            scene.Creator.Cancel();
            scene.Creator.Finish();
            scene.Creator.Begin();
            Assert.That(scene.Creator.IsCreating, Is.True);
            scene.Creator.Dispose();
            scene.Creator.Dispose();
            Assert.That(first.Node.IsDisposed, Is.False);
            Assert.That(scene.Creator.CurrentDetection.Node, Is.Null);
            Assert.Throws<ObjectDisposedException>(scene.Creator.Begin);
            Assert.Throws<ObjectDisposedException>(scene.Creator.Update);
            Assert.Throws<ObjectDisposedException>(() => scene.Creator.TryConfirm(out _));
            Assert.Throws<ObjectDisposedException>(scene.Creator.Cancel);
            Assert.Throws<ObjectDisposedException>(scene.Creator.Finish);
        }

        [Test]
        public void DisposedDetectionsAreRejectedAndDisposedAnchorIsReleasedOnUpdate()
        {
            // Creator table: stale selection and externally disposed anchor.
            using var scene = new NetworkCreatorScene();
            using var dead = new PlanarNode(Vector2.One);
            scene.Creator.Begin();
            scene.Creator.TryConfirm(out var first);
            first.Node.Dispose();
            scene.Creator.Update();
            Assert.That(scene.Creator.State, Is.EqualTo(LinearNetworkCreatorState.PutFirstNode));
            dead.Dispose();
            scene.Detection = new Detection<Vector2>(Vector2.One, dead);
            Assert.That(scene.Creator.TryConfirm(out _), Is.False);
            using var segment = new PlanarSegment();
            segment.Dispose();
            scene.Detection = new Detection<Vector2>(Vector2.One, segment: segment);
            Assert.That(scene.Creator.TryConfirm(out _), Is.False);
            Assert.That(scene.Nodes.Count, Is.EqualTo(1));
        }

        [Test]
        public void DetectionExceptionsAndReentrancyDoNotCorruptTheCreator()
        {
            // Creator table: provider failure, callback reentry, and recovery.
            using var scene = new NetworkCreatorScene();
            scene.Detecting = () => throw new InvalidOperationException();
            Assert.Throws<InvalidOperationException>(scene.Creator.Begin);
            Assert.That(scene.Creator.State, Is.EqualTo(LinearNetworkCreatorState.Idle));
            scene.Detecting = null;
            scene.Creator.Begin();
            scene.Detection = new Detection<Vector2>(Vector2.One);
            scene.Detecting = scene.Creator.Finish;
            Assert.Throws<InvalidOperationException>(scene.Creator.Update);
            Assert.That(scene.Creator.CurrentDetection.Position, Is.EqualTo(Vector2.Zero));
            scene.Detecting = null;
            scene.NodeCreated = scene.Creator.Cancel;
            Assert.Throws<InvalidOperationException>(() => scene.Creator.TryConfirm(out _));
            Assert.That(scene.Nodes.Single().IsDisposed, Is.True);
            scene.NodeCreated = null;
            scene.Creator.TryConfirm(out var first);
            scene.Detection = new Detection<Vector2>(new Vector2(2, 0));
            scene.SegmentCreated = scene.Creator.Dispose;
            Assert.Throws<InvalidOperationException>(() => scene.Creator.TryConfirm(out _));
            Assert.That(scene.Creator.IsDisposed, Is.False);
            Assert.That(scene.Creator.AnchorNode, Is.SameAs(first.Node));
            Assert.That(first.Node.Segments, Is.Empty);
        }

        [Test]
        public void SegmentDetectionCanResolveLocationInsideTheNodeFactory()
        {
            // Creator table: Position + Segment creates a Pin through the inverse location API.
            using var from = new PlanarNode(Vector2.Zero);
            using var to = new PlanarNode(new Vector2(10, 0));
            using var source = new PlanarSegment(from, to);
            var detection = new Detection<Vector2>(new Vector2(3, 0), segment: source);
            using var creator = new LinearGeometricNetworkCreator<Vector2>(
                () => detection,
                (Detection<Vector2> hit, out INode<Vector2> node) =>
                {
                    node = null;
                    if (!hit.Segment.TryGetLocation(hit.Position, out var location)) return false;
                    node = new SegmentPin<Vector2, float>((ISegment<Vector2, float>)hit.Segment, location);
                    return true;
                },
                (INode<Vector2> a, INode<Vector2> b, out ISegment<Vector2> segment) =>
                {
                    segment = null;
                    throw new AssertionException("The first anchor does not create a segment.");
                });

            creator.Begin();
            Assert.That(creator.TryConfirm(out var result), Is.True);
            using var pin = (SegmentPin<Vector2, float>)result.Node;
            Assert.That(pin.Location.Distance, Is.EqualTo(3f));
            Assert.That(pin.Position, Is.EqualTo(detection.Position));
            creator.Finish();
            Assert.That(pin.IsDisposed || source.IsDisposed, Is.False);
            Assert.That(source.From, Is.SameAs(from));
            Assert.That(source.To, Is.SameAs(to));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void InvalidSegmentFactoryResultsAreRejectedAndCleaned(int kind)
        {
            // Creator table: null, disconnected, and non-linear results violate the linear factory contract.
            using var a = new PlanarNode(Vector2.Zero);
            using var b = new PlanarNode(Vector2.One);
            var hit = new Detection<Vector2>(Vector2.Zero, a);
            ISegment<Vector2> returned = null;
            using var creator = new LinearGeometricNetworkCreator<Vector2>(
                () => hit,
                (Detection<Vector2> detection, out INode<Vector2> node) => { node = null; return false; },
                (INode<Vector2> from, INode<Vector2> to, out ISegment<Vector2> segment) =>
                {
                    segment = kind == 0 ? null : kind == 1 ? new PlanarSegment() : new Route<Vector2, float>();
                    returned = segment;
                    return true;
                });

            creator.Begin();
            creator.TryConfirm(out _);
            hit = new Detection<Vector2>(Vector2.One, b);
            Assert.Throws<InvalidOperationException>(() => creator.TryConfirm(out _));
            Assert.That(creator.AnchorNode, Is.SameAs(a));
            Assert.That(a.IsDisposed || b.IsDisposed, Is.False);
            if (returned != null) Assert.That(returned.IsDisposed, Is.True);
        }

        [Test]
        public void DependenciesAreRequiredAndInterfaceBranchesRemainSeparate()
        {
            // Creator table: construction and the agreed common/geometric/route hierarchy.
            using var scene = new NetworkCreatorScene();
            var creator = scene.Creator;
            Assert.Throws<ArgumentNullException>(() => new LinearGeometricNetworkCreator<Vector2>(
                null, creator.NodeFactory, creator.SegmentFactory));
            Assert.Throws<ArgumentNullException>(() => new LinearGeometricNetworkCreator<Vector2>(
                creator.GetCurrentDetections, null, creator.SegmentFactory));
            Assert.Throws<ArgumentNullException>(() => new LinearGeometricNetworkCreator<Vector2>(
                creator.GetCurrentDetections, creator.NodeFactory, null));
            Assert.That(creator, Is.InstanceOf<IGeometricNetworkCreator<Vector2>>());
            Assert.That(typeof(INetworkCreator<Vector2>)
                .IsAssignableFrom(typeof(IRouteNetworkCreator<Vector2>)), Is.True);
            Assert.That(typeof(IGeometricNetworkCreator<Vector2>)
                .IsAssignableFrom(typeof(IBezierGeometricNetworkCreator<Vector2>)), Is.True);
            Assert.That(typeof(IGeometricNetworkCreator<Vector2>)
                .IsAssignableFrom(typeof(IRouteNetworkCreator<Vector2>)), Is.False);
        }
    }
}
