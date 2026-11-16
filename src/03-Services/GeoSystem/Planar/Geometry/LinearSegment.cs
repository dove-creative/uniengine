using System;
using System.Numerics;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public class LinearSegment : Segment, ILinearSegment
    {
        // Internal
        private Vector2 _start;
        private Vector2 _end;
        private float _rotation;


        // Content
        public LinearSegment() { }
        public LinearSegment(INode from, INode to)
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

        protected override int OnNodeConnected(INavNode node)
        {
            node.EventHub.Connect(this)
                .OnUpdatedWhere(events => (events & (int)GeoEvents.Shape) != 0, () =>
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

        private void UpdateShape()
        {
            if (From == null || To == null)
            {
                Length = 0f;
                return;
            }

            _start = From.Position;
            _end = To.Position;
            var x = (double)_end.X - _start.X;
            var y = (double)_end.Y - _start.Y;
            Length = (float)System.Math.Sqrt(x * x + y * y);
            _rotation = (float)(System.Math.Atan2(y, x) * 180d / System.Math.PI);
        }

        public override bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null)
        {
            ThrowIfDisposed();
            var tolerance = epsilon ?? Epsilon;
            Location.ValidateEpsilon(tolerance);
            transform = default;
            if (From == null || To == null || Length <= tolerance
                || !location.TryResolve(Length, tolerance, out var distance)) return false;

            var rate = (double)distance / Length;
            var position = new Vector2(
                (float)(_start.X + ((double)_end.X - _start.X) * rate),
                (float)(_start.Y + ((double)_end.Y - _start.Y) * rate));
            transform = new Transform2D(position, _rotation + (location.IsForward ? 0f : 180f));
            return true;
        }
    }
}
