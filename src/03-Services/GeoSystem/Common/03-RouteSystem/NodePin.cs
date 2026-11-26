using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public class NodePin<TPosition> : NavNode, INode<TPosition>, INodePin, IPin<TPosition>
    {
        // Front
        public TPosition Position { get; protected set; }
        public new IEnumerable<ISegment<TPosition>> Segments => base.Segments.Cast<ISegment<TPosition>>();
        IEnumerable<IReadOnlySegment<TPosition>> IReadOnlyNode<TPosition>.Segments => Segments;
        public IReadOnlyNode<TPosition> AnchorNode { get; private set; }
        public bool IsValid => !IsDisposed && AnchorNode != null && !AnchorNode.IsDisposed
            && (AnchorNode is not RouteSystem.IPin pin || pin.IsValid);
        Geometry.IReadOnlyNode INodePin.AnchorNode => AnchorNode;


        // Content
        public NodePin() { }
        public NodePin(IReadOnlyNode<TPosition> anchor) => Connect(anchor);

        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment<TPosition>)
                throw new ArgumentException("The segment must use the node's position type.", nameof(segment));
        }

        public void Connect(IReadOnlyNode<TPosition> anchor)
        {
            ThrowIfDisposed();
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            if (anchor.IsDisposed || ReferenceEquals(anchor, this))
                throw new ArgumentException("The anchor must be a live, different node.", nameof(anchor));
            if (ReferenceEquals(anchor, AnchorNode)) return;
            ValidateAnchor(anchor);

            AnchorNode?.EventHub.Disconnect(this);
            AnchorNode = anchor;
            anchor.EventHub.Connect(this)
                .OnUpdatedWhere(events => (events & (int)GeoEvents.Shape) != 0, UpdatePosition)
                .OnDisposing(Dispose);
            UpdatePosition();
        }

        void INodePin.Connect(Geometry.IReadOnlyNode anchor) => Connect(
            anchor as IReadOnlyNode<TPosition>
                ?? throw new ArgumentException("The anchor must use the same position type.", nameof(anchor)));

        protected virtual void ValidateAnchor(IReadOnlyNode<TPosition> anchor) =>
            GeoDependency.Validate(this, anchor);

        public void Disconnect()
        {
            if (AnchorNode == null) return;
            AnchorNode.EventHub.Disconnect(this);
            AnchorNode = null;
            OnUpdated((int)GeoEvents.Shape);
        }

        private void UpdatePosition()
        {
            Position = AnchorNode.Position;
            OnUpdated((int)GeoEvents.Shape);
        }

        protected override void OnDisposing() => Disconnect();
    }
}
