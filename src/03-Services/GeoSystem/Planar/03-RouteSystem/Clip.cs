using System;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Objects;
using IReadOnlySegment = BlackThunder.GeoSystem.Geometry.Planar.IReadOnlySegment;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class Clip : Segment, IClip
    {
        // Front
        public new SegmentPin From => (SegmentPin)base.From;
        public new SegmentPin To => (SegmentPin)base.To;
        public IReadOnlySegment SegmentSource { get; private set; }
        public bool IsForward { get; private set; } = true;
        public bool IsValid { get; private set; }
        ISegmentPin IClip.From => From;
        ISegmentPin IClip.To => To;
        Geometry.IReadOnlySegment IClip.SegmentSource => SegmentSource;

        // Internal
        private readonly object _sourceListener = new();
        private float _startDistance;


        // Content
        public Clip() { }
        public Clip(SegmentPin from, SegmentPin to)
        {
            try
            {
                if (from != null) ConnectNode(ConnectionSide.From, from);
                if (to != null) ConnectNode(ConnectionSide.To, to);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        protected override void OnNodeConnecting(ConnectionSide side, INavNode node)
        {
            base.OnNodeConnecting(side, node);
            if (node is not SegmentPin pin)
                throw new ArgumentException("A clip endpoint must be a planar segment pin.", nameof(node));
            if (ReferenceEquals(pin.AnchorSegment, this))
                throw new ArgumentException("A clip cannot use itself as its source.", nameof(node));
        }

        protected override int OnNodeConnected(INavNode node)
        {
            node.EventHub.Connect(this)
                .OnUpdatedWhere(events => (events & (int)(RouteEvents.Shape | RouteEvents.Location)) != 0, () =>
                {
                    UpdateShape();
                    OnUpdated((int)GeoEvents.Shape);
                });
            UpdateShape();
            return (int)GeoEvents.Shape;
        }

        protected override int OnNodeDisconnected(INavNode node)
        {
            node.EventHub.Disconnect(this);
            UpdateShape();
            return (int)GeoEvents.Shape;
        }

        protected override void OnToleranceChanged()
        {
            UpdateShape();
            OnUpdated((int)GeoEvents.Shape);
        }

        private void UpdateShape()
        {
            if (SegmentSource != null && SegmentSource.IsDisposed)
            {
                Dispose();
                return;
            }

            var source = From?.AnchorSegment;
            if (source == null || !ReferenceEquals(source, To?.AnchorSegment) || ReferenceEquals(source, this))
                source = null;

            if (!ReferenceEquals(source, SegmentSource))
            {
                SegmentSource?.EventHub.Disconnect(_sourceListener);
                SegmentSource = source;
                if (source != null && !source.IsDisposed)
                    source.EventHub.Connect(_sourceListener).OnDisposing(Dispose);
            }

            IsValid = false;
            Length = 0f;
            if (source == null || source.IsDisposed || From.IsDisposed || To.IsDisposed) return;
            if (!From.Location.TryResolve(source.Length, Epsilon, out var start)
                || !To.Location.TryResolve(source.Length, Epsilon, out var end)
                || !source.TryGetPosition(From.Location, out _, Epsilon)
                || !source.TryGetPosition(To.Location, out _, Epsilon)) return;

            var length = System.Math.Abs(end - start);
            if (length <= Epsilon) return;

            _startDistance = start;
            IsForward = start < end;
            Length = length;
            IsValid = true;
        }

        public override bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null)
        {
            ThrowIfDisposed();
            var tolerance = epsilon ?? Epsilon;
            Location.ValidateEpsilon(tolerance);
            transform = default;
            if (!IsValid || !location.TryResolve(Length, tolerance, out var distance)) return false;

            var sourceDistance = _startDistance + (IsForward ? distance : -distance);
            var sourceForward = IsForward == location.IsForward;
            var directedDistance = sourceForward ? sourceDistance : SegmentSource.Length - sourceDistance;
            return SegmentSource.TryGetTransform(
                new Location(directedDistance, DistanceType.Length, sourceForward), out transform, tolerance);
        }

        public void Clear() => ClearNodes();

        protected override void OnDisposing()
        {
            SegmentSource?.EventHub.Disconnect(_sourceListener);
            SegmentSource = null;
            IsValid = false;
            Length = 0f;
        }
    }
}
