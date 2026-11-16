using System;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class NodePin : PositionedNode, INodePin, IPin
    {
        // Front
        public IReadOnlyNode AnchorNode { get; private set; }
        public bool IsValid => !IsDisposed && AnchorNode != null && !AnchorNode.IsDisposed
            && (AnchorNode is not RouteSystem.IPin pin || pin.IsValid);
        Geometry.IReadOnlyNode INodePin.AnchorNode => AnchorNode;


        // Content
        public NodePin() { }
        public NodePin(IReadOnlyNode anchor) => Connect(anchor);

        public void Connect(IReadOnlyNode anchor)
        {
            ThrowIfDisposed();
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            if (anchor.IsDisposed || ReferenceEquals(anchor, this))
                throw new ArgumentException("The anchor must be a live, different node.", nameof(anchor));
            if (ReferenceEquals(anchor, AnchorNode)) return;
            GeoDependency.Validate(this, anchor);

            AnchorNode?.EventHub.Disconnect(this);
            AnchorNode = anchor;
            anchor.EventHub.Connect(this)
                .OnUpdatedWhere(events => (events & (int)GeoEvents.Shape) != 0, UpdatePosition)
                .OnDisposing(Dispose);
            UpdatePosition();
        }

        void INodePin.Connect(Geometry.IReadOnlyNode anchor) => Connect(
            anchor as IReadOnlyNode ?? throw new ArgumentException("A planar anchor is required.", nameof(anchor)));

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
