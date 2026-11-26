using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public class Hold<TPosition, TRotation> : NavNode, INode<TPosition>, IHold, IPin<TPosition>
    {
        // Front
        public TPosition Position { get; protected set; }
        public new IEnumerable<ISegment<TPosition>> Segments => base.Segments.Cast<ISegment<TPosition>>();
        IEnumerable<IReadOnlySegment<TPosition>> IReadOnlyNode<TPosition>.Segments => Segments;
        public IPin<TPosition> Anchor { get; private set; }
        public bool IsValid => !IsDisposed && Anchor != null && Anchor.IsValid;
        RouteSystem.IPin IHold.Anchor => Anchor;


        // Content
        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment<TPosition>)
                throw new ArgumentException("The segment must use the node's position type.", nameof(segment));
        }

        public void MoveTo(INode<TPosition> node)
        {
            ThrowIfDisposed();
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.IsDisposed || ReferenceEquals(node, this) || ReferenceEquals(node, Anchor))
                throw new ArgumentException("The anchor must be an independent live node.", nameof(node));
            ValidateNode(node);

            if (Anchor is NodePin<TPosition> pin) pin.Connect(node);
            else ReplaceAnchor(CreateNodePin(node));
        }

        public void MoveTo(ISegment<TPosition, TRotation> segment, Location location)
        {
            ThrowIfDisposed();
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            if (segment.IsDisposed) throw new ArgumentException("The segment is disposed.", nameof(segment));
            ValidateSegment(segment);

            if (Anchor is SegmentPin<TPosition, TRotation> pin) pin.Connect(segment, location);
            else ReplaceAnchor(CreateSegmentPin(segment, location));
        }

        void IHold.MoveTo(Geometry.INode node) => MoveTo(
            node as INode<TPosition>
                ?? throw new ArgumentException("The node must use the same position type.", nameof(node)));
        void IHold.MoveTo(Geometry.ISegment segment, Location location) => MoveTo(
            segment as ISegment<TPosition, TRotation>
                ?? throw new ArgumentException("The segment must use matching P and R types.", nameof(segment)),
            location);

        protected virtual void ValidateNode(INode<TPosition> node) => GeoDependency.Validate(this, node);
        protected virtual void ValidateSegment(ISegment<TPosition, TRotation> segment) =>
            GeoDependency.Validate(this, segment);

        protected virtual NodePin<TPosition> CreateNodePin(IReadOnlyNode<TPosition> node) => new(node);
        protected virtual SegmentPin<TPosition, TRotation> CreateSegmentPin(
            IReadOnlySegment<TPosition, TRotation> segment, Location location) => new(segment, location);

        private void ReplaceAnchor(IPin<TPosition> anchor)
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
