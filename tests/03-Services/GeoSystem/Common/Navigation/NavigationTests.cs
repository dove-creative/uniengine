using System;
using System.Linq;
using BlackThunder.GeoSystem.Navigation;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    public class NavigationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ConnectionNotificationsSeeBothSidesCommitted(bool throughNode)
        {
            using var from = new NavNode();
            using var to = new NavNode();
            using var segment = new NavSegment(4f);
            var nodeEvents = 0;
            var segmentEvents = 0;
            from.EventHub.Connect(new object()).OnUpdated(events =>
            {
                nodeEvents |= events;
                Assert.That(from.Segments.Contains(segment), Is.EqualTo(ReferenceEquals(segment.From, from)));
            });
            segment.EventHub.Connect(new object()).OnUpdated(events =>
            {
                segmentEvents |= events;
                Assert.That(from.Segments.Contains(segment), Is.EqualTo(ReferenceEquals(segment.From, from)));
            });
            if (throughNode) from.ConnectSegment(segment, ConnectionSide.From);
            else segment.ConnectNode(ConnectionSide.From, from);
            segment.ConnectNode(ConnectionSide.To, to);
            Assert.That(segment.Nodes, Is.EqualTo(new[] { from, to }));
            Assert.That(segment.GetCounterNode(from), Is.SameAs(to));
            Assert.That(segment.GetCounterNode(to), Is.SameAs(from));
            Assert.That(segment.IsFromNode(from), Is.True);
            Assert.That(segment.IsFromNode(to), Is.False);
            if (throughNode) from.DisconnectSegment(segment);
            else segment.DisconnectNode(ConnectionSide.From);
            segment.DisconnectNode(to);
            Assert.That(segment.IsIsolated, Is.True);
            Assert.That(to.IsIsolated, Is.True);
            Assert.That(nodeEvents, Is.EqualTo((int)GeoEvents.AnyConnection));
            Assert.That(segmentEvents, Is.EqualTo((int)GeoEvents.AnyConnection));
        }

        [Test]
        public void RejectedConnectionsPreserveTheGraph()
        {
            using var from = new NavNode();
            using var other = new NavNode();
            using var disposed = new NavNode();
            using var segment = new NavSegment(from, null);
            disposed.Dispose();
            segment.ConnectNode(ConnectionSide.From, from);
            Assert.Throws<ArgumentException>(() => from.ConnectSegment(segment, ConnectionSide.From));
            Assert.Throws<InvalidOperationException>(() => segment.ConnectNode(ConnectionSide.From, other));
            Assert.Throws<ArgumentException>(() => segment.ConnectNode(ConnectionSide.To, from));
            Assert.Throws<ArgumentException>(() => segment.ConnectNode(ConnectionSide.To, disposed));
            Assert.Throws<ArgumentNullException>(() => segment.ConnectNode(ConnectionSide.To, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => segment.ConnectNode((ConnectionSide)2, other));
            Assert.That(segment.From, Is.SameAs(from));
            Assert.That(segment.To, Is.Null);
            Assert.That(from.Segments, Is.EqualTo(new[] { segment }));
            Assert.That(other.IsIsolated, Is.True);
            segment.DisconnectNode(other);
            segment.DisconnectNode((INavNode)null);
            Assert.Throws<ArgumentNullException>(() => segment.IsFromNode(null));
            Assert.Throws<ArgumentException>(() => segment.GetCounterNode(other));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearAndDisposeReleaseReciprocalReferences(bool dispose)
        {
            using var node = new NavNode();
            using var first = new NavSegment(node, null);
            using var second = new NavSegment(null, node);
            if (dispose) node.Dispose();
            else node.ClearSegments();
            Assert.That(node.IsIsolated, Is.True);
            Assert.That(first.IsIsolated && second.IsIsolated, Is.True);
            Assert.That(first.IsDisposed || second.IsDisposed, Is.False);
            if (dispose)
                Assert.Throws<ObjectDisposedException>(() => node.ConnectSegment(first, ConnectionSide.From));
            else
            {
                node.ConnectSegment(first, ConnectionSide.From);
                first.ClearNodes();
                node.ConnectSegment(first, ConnectionSide.From);
                first.Dispose();
                Assert.That(node.IsIsolated, Is.True);
                Assert.Throws<ObjectDisposedException>(() => first.ConnectNode(ConnectionSide.From, node));
            }
        }

        [Test]
        public void CallbackFailureDoesNotLeaveDisposedNodeConnected()
        {
            using var node = new NavNode();
            using var first = new NavSegment(node, null);
            using var second = new NavSegment(null, node);
            first.EventHub.Connect(new object()).OnUpdated(_ =>
                throw new InvalidOperationException("Observer failed."));
            Assert.Throws<AggregateException>(node.Dispose);
            Assert.That(node.IsDisposed, Is.True);
            Assert.That(node.Segments, Is.Empty);
            Assert.That(first.IsIsolated && second.IsIsolated, Is.True);
        }

        [Test]
        public void PublicContractsHideInternalConnectionMethods()
        {
            foreach (var type in new[] { typeof(INavNode), typeof(INavSegment), typeof(NavNode), typeof(NavSegment) })
                Assert.That(type.GetMethods().Any(method => method.Name.EndsWith("Internal")), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavSegment(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavSegment(-1f));
        }
    }
}
