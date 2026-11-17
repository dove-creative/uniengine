using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
        public PolySegment() : base(new Shapers.FreeHandle()) { }
        public PolySegment(IShaper<Vector2> shaper) : base(shaper) { }
        public PolySegment(INode from, INode to, IShaper<Vector2> shaper = null)
            : base(shaper ?? new Shapers.FreeHandle())
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

        protected override void ValidateHandle(SegmentHandle<Vector2> handle)
        {
            base.ValidateHandle(handle);
            if (!ShapeMath.IsFinite(handle.Position))
                throw new ArgumentException("The handle position must be finite.", nameof(handle));
        }

        // Shape
        protected override bool TryBuildHinges(
            IReadOnlyList<Vector2> positions,
            List<Hinge<Vector2, float>> hinges,
            out float length)
        {
            length = 0f;
            var points = new List<Vector2>();
            foreach (var point in positions)
            {
                if (!ShapeMath.IsFinite(point)) return false;
                if (points.Count == 0 || points[points.Count - 1] != point) points.Add(point);
            }
            if (points.Count < 2) return false;

            double distance = 0;
            float rotation = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                rotation = ShapeMath.Rotation(points[i], points[i + 1]);
                var start = (float)distance;
                distance += ShapeMath.Distance(points[i], points[i + 1]);
                length = (float)distance;
                if (!Location.IsFinite(length) || length <= start) return false;
                hinges.Add(new Hinge<Vector2, float>(points[i], rotation, start));
            }
            hinges.Add(new Hinge<Vector2, float>(points[points.Count - 1], rotation, length));
            return true;
        }

        // Queries
        public override bool TryGetTransform(
            Location location,
            out Vector2 position,
            out float rotation,
            float? epsilon = null)
        {
            ThrowIfDisposed();
            var tolerance = epsilon ?? Epsilon;
            Location.ValidateEpsilon(tolerance);
            position = default;
            rotation = default;
            if (Hinges.Count < 2 || Length <= tolerance
                || !location.TryResolve(Length, tolerance, out var distance)) return false;

            var low = 0;
            var high = Hinges.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (Hinges[middle].Distance <= distance) low = middle + 1;
                else high = middle;
            }
            var index = Math.Max(0, low - 1);
            if (!location.IsForward && index > 0 && Hinges[index].Distance == distance) index--;
            index = Math.Min(index, Hinges.Count - 2);
            var start = Hinges[index];
            var end = Hinges[index + 1];
            var rate = ((double)distance - start.Distance) / ((double)end.Distance - start.Distance);
            position = ShapeMath.Lerp(start.Position, end.Position, rate);
            rotation = start.Rotation + (location.IsForward ? 0f : 180f);
            return true;
        }

        public bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null)
        {
            var success = TryGetTransform(location, out var position, out var rotation, epsilon);
            transform = success ? new Transform2D(position, rotation) : default;
            return success;
        }
    }
}
