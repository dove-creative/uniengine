using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Pathfinders;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Mathematics;
using BlackThunder.UniEngine.Objects;
using INode = BlackThunder.GeoSystem.Geometry.Planar.INode;
using ISegment = BlackThunder.GeoSystem.Geometry.Planar.ISegment;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class Route : RouteSystem.Route, ISegment
    {
        // Front
        public new INode From => (INode)base.From;
        public new INode To => (INode)base.To;
        public new IEnumerable<INode> Nodes => base.Nodes.Cast<INode>();
        public IDictionary<string, float> Values => EmptySegmentInputs<Vector2>.Values;
        public IList<SegmentHandle<Vector2>> Handles => EmptySegmentInputs<Vector2>.Handles;
        public float Epsilon
        {
            get => _epsilon;
            set
            {
                ThrowIfDisposed();
                Location.ValidateEpsilon(value);
                if (_epsilon == value) return;
                _epsilon = value;
                Notify((int)GeoEvents.Shape);
            }
        }

        // Internal
        private float _epsilon = UniMath.Epsilon;


        // Content
        public Route() : base(new AStar<Vector2>(Vector2.Distance)) { }
        public Route(IPathfinder pathfinder) : base(pathfinder) { }

        protected override void ValidateNode(Geometry.INode node)
        {
            if (node is not INode)
                throw new ArgumentException("A planar route requires a planar node.", nameof(node));
            base.ValidateNode(node);
        }

        protected override void ValidateSource(Geometry.ISegment source)
        {
            if (source is not ISegment)
                throw new ArgumentException("A planar route requires a planar segment.", nameof(source));
            base.ValidateSource(source);
        }

        public bool TryGetPosition(Location location, out Vector2 position, float? epsilon = null)
        {
            var success = TryGetTransform(location, out var transform, epsilon);
            position = success ? transform.Position : default;
            return success;
        }

        public bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null)
        {
            ThrowIfDisposed();
            var tolerance = epsilon ?? Epsilon;
            Location.ValidateEpsilon(tolerance);
            transform = default;
            if (Length <= tolerance || !location.TryResolve(Length, tolerance, out var distance)) return false;

            var remaining = location.IsForward ? distance : Length - distance;
            for (int offset = 0; offset < Views.Count; offset++)
            {
                var index = location.IsForward ? offset : Views.Count - 1 - offset;
                var view = Views[index];
                var source = (ISegment)view.Source;
                if (source.Length <= 0f) continue;
                if (remaining > (double)source.Length + tolerance)
                {
                    remaining -= source.Length;
                    continue;
                }

                return source.TryGetTransform(
                    new Location(remaining, DistanceType.Length, view.IsForward == location.IsForward),
                    out transform,
                    tolerance);
            }

            return false;
        }
    }
}
