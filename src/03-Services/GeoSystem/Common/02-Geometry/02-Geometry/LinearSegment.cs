using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Mathematics;
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
            Length = (float)UniMath.Distance(_start, _end);
            _rotation = UniMath.Rotation(_start, _end);
        }

        public override bool TryGetLocation(
            Vector2 position,
            float startDistance,
            float endDistance,
            out Location location,
            out double errorSquared,
            bool isForward = true,
            DistanceType distanceType = DistanceType.Length,
            float? epsilon = null)
        {
            ThrowIfDisposed();
            var query = new LocationQuery(Length, startDistance, endDistance,
                isForward, distanceType, epsilon ?? Epsilon);
            if (query.HasValidLength && From != null && To != null && UniMath.IsFinite(position))
                LocationProjection.AddLine(ref query, position, _start, _end, 0, Length);
            return query.TryGetResult(out location, out errorSquared);
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
            var position = UniMath.Lerp(_start, _end, rate);
            transform = new Transform2D(position, _rotation + (location.IsForward ? 0f : 180f));
            return true;
        }
    }
}
