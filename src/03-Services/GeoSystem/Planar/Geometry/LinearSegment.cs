using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public class LinearSegment : Geometry.LinearSegment<Vector2, float>, ILinearSegment
    {
        // Front
        public new INode From => (INode)base.From;
        public new INode To => (INode)base.To;
        public new IEnumerable<INode> Nodes => base.Nodes.Cast<INode>();

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

        protected override void OnNodeConnecting(ConnectionSide side, INavNode node)
        {
            if (node is not INode)
                throw new ArgumentException("A planar segment requires a planar node.", nameof(node));
            base.OnNodeConnecting(side, node);
        }

        protected override void UpdateShape()
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

        public override bool TryGetTransform(
            Location location,
            out Vector2 position,
            out float rotation,
            float? epsilon = null)
        {
            var success = TryGetTransform(location, out var transform, epsilon);
            position = success ? transform.Position : default;
            rotation = success ? transform.Rotation : default;
            return success;
        }

        public bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null)
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
