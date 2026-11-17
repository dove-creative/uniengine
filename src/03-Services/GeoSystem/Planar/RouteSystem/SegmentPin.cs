using System;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.GeoSystem.Navigation;
using IReadOnlySegment = BlackThunder.GeoSystem.Geometry.Planar.IReadOnlySegment;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class SegmentPin : PositionedNode, ISegmentPin, IPin
    {
        // Front
        public IReadOnlySegment AnchorSegment { get; private set; }
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
        public SegmentPin(IReadOnlySegment anchor, Location location = default) => Connect(anchor, location);

        public void Connect(IReadOnlySegment anchor, Location? location = null)
        {
            ThrowIfDisposed();
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            if (anchor.IsDisposed) throw new ArgumentException("The anchor is disposed.", nameof(anchor));
            GeoDependency.Validate(this, anchor);

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
            anchor as IReadOnlySegment ?? throw new ArgumentException("A planar anchor is required.", nameof(anchor)),
            location);

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
            var position = default(System.Numerics.Vector2);
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
