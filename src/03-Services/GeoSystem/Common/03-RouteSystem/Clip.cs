using System;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public class Clip<TPosition, TRotation> : Segment<TPosition, TRotation>, IClip, ITerminalTolerance
    {
        // Front
        public new SegmentPin<TPosition, TRotation> From => (SegmentPin<TPosition, TRotation>)base.From;
        public new SegmentPin<TPosition, TRotation> To => (SegmentPin<TPosition, TRotation>)base.To;
        public IReadOnlySegment<TPosition, TRotation> SegmentSource { get; private set; }
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
        public Clip(SegmentPin<TPosition, TRotation> from, SegmentPin<TPosition, TRotation> to)
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
            if (node is not SegmentPin<TPosition, TRotation> pin)
                throw new ArgumentException(
                    "A clip endpoint must be a segment pin with matching P and R types.", nameof(node));
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

        public override bool TryGetLocation(
            TPosition position,
            float startDistance,
            float endDistance,
            out Location location,
            out double errorSquared,
            bool isForward = true,
            DistanceType distanceType = DistanceType.Length,
            float? epsilon = null) =>
            TerminalPath.For(this).TryGetLocation(position, startDistance, endDistance,
                out location, out errorSquared, isForward, distanceType, epsilon);

        public override bool TryGetTransform(
            Location location, out TPosition position, out TRotation rotation, float? epsilon = null)
        {
            ThrowIfDisposed();
            var tolerance = epsilon ?? Epsilon;
            Location.ValidateEpsilon(tolerance);
            position = default;
            rotation = default;
            if (!IsValid || !location.TryResolve(Length, tolerance, out var distance)) return false;

            var sourceDistance = _startDistance + (IsForward ? distance : -distance);
            var sourceForward = IsForward == location.IsForward;
            var directedDistance = sourceForward ? sourceDistance : SegmentSource.Length - sourceDistance;
            return SegmentSource.TryGetTransform(
                new Location(directedDistance, DistanceType.Length, sourceForward),
                out position, out rotation, tolerance);
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
