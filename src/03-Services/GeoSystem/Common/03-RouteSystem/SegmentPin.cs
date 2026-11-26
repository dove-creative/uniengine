using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public class SegmentPin<TPosition, TRotation> : NavNode, INode<TPosition>, ISegmentPin, IPin<TPosition>
    {
        // Front
        public TPosition Position { get; protected set; }
        public new IEnumerable<ISegment<TPosition>> Segments => base.Segments.Cast<ISegment<TPosition>>();
        IEnumerable<IReadOnlySegment<TPosition>> IReadOnlyNode<TPosition>.Segments => Segments;
        public IReadOnlySegment<TPosition, TRotation> AnchorSegment { get; private set; }
        public bool IsValid { get; private set; }
        public Location Location
        {
            get => _location;
            set
            {
                ThrowIfDisposed();
                if (_location == value) return;
                _location = value;
                UpdatePosition((int)RouteEvents.Location);
            }
        }
        Geometry.IReadOnlySegment ISegmentPin.AnchorSegment => AnchorSegment;

        // Internal
        private Location _location;


        // Content
        public SegmentPin() { }
        public SegmentPin(IReadOnlySegment<TPosition, TRotation> anchor, Location location = default) =>
            Connect(anchor, location);

        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment<TPosition>)
                throw new ArgumentException("The segment must use the node's position type.", nameof(segment));
        }

        public void Connect(IReadOnlySegment<TPosition, TRotation> anchor, Location? location = null)
        {
            ThrowIfDisposed();
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            if (anchor.IsDisposed) throw new ArgumentException("The anchor is disposed.", nameof(anchor));
            ValidateAnchor(anchor);

            if (!ReferenceEquals(anchor, AnchorSegment))
            {
                AnchorSegment?.EventHub.Disconnect(this);
                AnchorSegment = anchor;
                anchor.EventHub.Connect(this)
                    .OnUpdatedWhere(
                        events => (events & (int)(GeoEvents.AnyConnection | GeoEvents.Shape)) != 0,
                        () => UpdatePosition(0))
                    .OnDisposing(Dispose);
            }

            if (location.HasValue) _location = location.Value;
            UpdatePosition((int)RouteEvents.Location);
        }

        void ISegmentPin.Connect(Geometry.IReadOnlySegment anchor, Location? location) => Connect(
            anchor as IReadOnlySegment<TPosition, TRotation>
                ?? throw new ArgumentException("The anchor must use matching P and R types.", nameof(anchor)),
            location);

        protected virtual void ValidateAnchor(IReadOnlySegment<TPosition, TRotation> anchor) =>
            GeoDependency.Validate(this, anchor);

        public void Disconnect()
        {
            if (AnchorSegment == null) return;
            AnchorSegment.EventHub.Disconnect(this);
            AnchorSegment = null;
            IsValid = false;
            OnUpdated((int)(RouteEvents.Location | RouteEvents.Shape));
        }

        private void UpdatePosition(int events)
        {
            var position = default(TPosition);
            IsValid = AnchorSegment != null && !AnchorSegment.IsDisposed
                && AnchorSegment.TryGetPosition(_location, out position);
            if (IsValid) Position = position;
            OnUpdated(events | (int)RouteEvents.Shape);
        }

        protected override void OnDisposing()
        {
            Disconnect();
            IsValid = false;
        }
    }
}
