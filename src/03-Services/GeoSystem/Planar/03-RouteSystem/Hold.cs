using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.GeoSystem.Navigation;
using INode = BlackThunder.GeoSystem.Geometry.Planar.INode;
using ISegment = BlackThunder.GeoSystem.Geometry.Planar.ISegment;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class Hold : NavNode, INode, IHold, IPin
    {
        public Vector2 Position { get; protected set; }
        public new IEnumerable<ISegment> Segments => base.Segments.Cast<ISegment>();
        public IPin Anchor { get; private set; }
        public bool IsValid => !IsDisposed && Anchor != null && Anchor.IsValid;
        RouteSystem.IPin IHold.Anchor => Anchor;


        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment)
                throw new ArgumentException("A planar node requires a planar segment.", nameof(segment));
        }

        public void MoveTo(INode node)
        {
            ThrowIfDisposed();
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.IsDisposed || ReferenceEquals(node, this) || ReferenceEquals(node, Anchor))
                throw new ArgumentException("The anchor must be an independent live node.", nameof(node));
            GeoDependency.Validate(this, node);

            if (Anchor is NodePin pin) pin.Connect(node);
            else ReplaceAnchor(new NodePin(node));
        }

        public void MoveTo(ISegment segment, Location location)
        {
            ThrowIfDisposed();
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            if (segment.IsDisposed) throw new ArgumentException("The segment is disposed.", nameof(segment));
            GeoDependency.Validate(this, segment);

            if (Anchor is SegmentPin pin) pin.Connect(segment, location);
            else ReplaceAnchor(new SegmentPin(segment, location));
        }

        void IHold.MoveTo(Geometry.INode node) => MoveTo(
            node as INode ?? throw new ArgumentException("A planar node is required.", nameof(node)));
        void IHold.MoveTo(Geometry.ISegment segment, Location location) => MoveTo(
            segment as ISegment ?? throw new ArgumentException("A planar segment is required.", nameof(segment)),
            location);

        private void ReplaceAnchor(IPin anchor)
        {
            var previous = Anchor;
            previous?.EventHub.Disconnect(this);
            Anchor = anchor;
            previous?.Dispose();
            anchor.EventHub.Connect(this)
                .OnUpdatedWhere(events => (events & (int)GeoEvents.Shape) != 0, UpdatePosition)
                .OnDisposing(Dispose);
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            Position = Anchor.Position;
            OnUpdated((int)GeoEvents.Shape);
        }

        protected override void OnDisposing()
        {
            var anchor = Anchor;
            Anchor = null;
            anchor?.EventHub.Disconnect(this);
            anchor?.Dispose();
        }
    }
}
