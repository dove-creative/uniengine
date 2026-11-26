using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.UniEngine.Mathematics;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public class PolySegment : Geometry.PolySegment<Vector2, float>, IPolySegment
    {
        // Front
        public new INode From => (INode)base.From;
        public new INode To => (INode)base.To;
        public new IEnumerable<INode> Nodes => base.Nodes.Cast<INode>();


        // Content --------------------
        // Construction
        public PolySegment() : base(new PolylineShaper()) { }
        public PolySegment(IShaper<Vector2> shaper) : base(shaper) { }
        public PolySegment(INode from, INode to, IShaper<Vector2> shaper = null)
            : base(shaper ?? new PolylineShaper())
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

        protected override void ValidateHandle(ControlHandle<Vector2> handle)
        {
            base.ValidateHandle(handle);
            if (!UniMath.IsFinite(handle.Position))
                throw new ArgumentException("The handle position must be finite.", nameof(handle));
        }

        // Shape
        private protected override bool TryBuildHinges(
            IReadOnlyList<Vector2> positions,
            List<Hinge<Vector2, float>> hinges,
            out float length)
        {
            length = 0f;
            var points = new List<Vector2>();
            foreach (var point in positions)
            {
                if (!UniMath.IsFinite(point)) return false;
                if (points.Count == 0 || points[points.Count - 1] != point) points.Add(point);
            }
            if (points.Count < 2) return false;

            double distance = 0;
            float rotation = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                rotation = UniMath.Rotation(points[i], points[i + 1]);
                var start = (float)distance;
                distance += UniMath.Distance(points[i], points[i + 1]);
                length = (float)distance;
                if (!Location.IsFinite(length) || length <= start) return false;
                hinges.Add(new Hinge<Vector2, float>(points[i], rotation, start));
            }
            hinges.Add(new Hinge<Vector2, float>(points[points.Count - 1], rotation, length));
            return true;
        }

        // Queries
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
            if (query.HasValidLength && UniMath.IsFinite(position))
            {
                for (int i = 0; i < Hinges.Count - 1; i++)
                {
                    var from = Hinges[i];
                    var to = Hinges[i + 1];
                    LocationProjection.AddLine(ref query, position,
                        from.Position, to.Position, from.Distance, to.Distance);
                }
            }
            return query.TryGetResult(out location, out errorSquared);
        }

        public override bool TryGetTransform(
            Location location,
            out Vector2 position,
            out float rotation,
            float? epsilon = null)
        {
            int index = -1;
            return ReadTransform(location, ref index, out position, out rotation, epsilon);
        }

        internal override bool TryGetTransformWithHint(Location location, ref int index,
            out Vector2 position, out float rotation, float? epsilon) =>
            GetType() == typeof(PolySegment)
                ? ReadTransform(location, ref index, out position, out rotation, epsilon)
                : TryGetTransform(location, out position, out rotation, epsilon);

        private bool ReadTransform(Location location, ref int index, out Vector2 position, out float rotation,
            float? epsilon)
        {
            ThrowIfDisposed();
            var tolerance = epsilon ?? Epsilon;
            Location.ValidateEpsilon(tolerance);
            position = default;
            rotation = default;
            if (Hinges.Count < 2 || Length <= tolerance
                || !location.TryResolve(Length, tolerance, out var distance)) return false;

            if (!ContainsDistance(index, distance, location.IsForward))
            {
                int low = 0, high = Hinges.Count - 1;
                while (low < high)
                {
                    var middle = low + (high - low) / 2;
                    var endDistance = Hinges[middle + 1].Distance;
                    if (distance > endDistance || (location.IsForward && distance == endDistance)) low = middle + 1;
                    else high = middle;
                }
                index = Math.Min(low, Hinges.Count - 2);
            }
            var start = Hinges[index];
            var end = Hinges[index + 1];
            var rate = ((double)distance - start.Distance) / ((double)end.Distance - start.Distance);
            position = UniMath.Lerp(start.Position, end.Position, rate);
            rotation = start.Rotation + (location.IsForward ? 0f : 180f);
            return true;
        }

        private bool ContainsDistance(int index, float distance, bool forward)
        {
            if ((uint)index >= (uint)(Hinges.Count - 1)) return false;
            var start = Hinges[index].Distance;
            var end = Hinges[index + 1].Distance;
            return (distance > start || (distance == start && (forward || index == 0)))
                && (distance < end || (distance == end && (!forward || index == Hinges.Count - 2)));
        }

        public bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null)
        {
            var success = TryGetTransform(location, out var position, out var rotation, epsilon);
            transform = success ? new Transform2D(position, rotation) : default;
            return success;
        }
    }
}
