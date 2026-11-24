using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
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
        public IList<ControlHandle<Vector2>> Handles => EmptySegmentInputs<Vector2>.Handles;
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
        internal override float TerminalEpsilon => Epsilon;


        // Content
        public Route() : this(new AStarPathfinder<Vector2>(Vector2.Distance)) { }
        public Route(IPathfinder<Vector2> pathfinder) : base(Bind(pathfinder)) { }

        private static FindPath Bind(IPathfinder<Vector2> pathfinder)
        {
            if (pathfinder == null) throw new ArgumentNullException(nameof(pathfinder));
            return (Geometry.INode from, Geometry.INode to, out Navigation.Path path) =>
            {
                var success = pathfinder.TryPathfind((INode)from, new[] { (INode)to }, out var typed);
                path = typed;
                return success;
            };
        }

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
            var tolerance = epsilon ?? Epsilon;
            transform = default;
            return TryGetTerminalSegment(location, out var segment, out var local, tolerance)
                && ((Geometry.Planar.IReadOnlySegment)segment).TryGetTransform(local, out transform, tolerance);
        }
    }
}
