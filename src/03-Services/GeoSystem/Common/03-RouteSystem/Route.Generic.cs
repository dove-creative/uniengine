using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Mathematics;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public class Route<TPosition, TRotation> : Route, ISegment<TPosition, TRotation>
    {
        // Front
        public new INode<TPosition> From => (INode<TPosition>)base.From;
        public new INode<TPosition> To => (INode<TPosition>)base.To;
        public new IEnumerable<INode<TPosition>> Nodes => base.Nodes.Cast<INode<TPosition>>();
        IReadOnlyNode<TPosition> IReadOnlySegment<TPosition>.From => From;
        IReadOnlyNode<TPosition> IReadOnlySegment<TPosition>.To => To;
        IEnumerable<IReadOnlyNode<TPosition>> IReadOnlySegment<TPosition>.Nodes => Nodes;
        public IDictionary<string, float> Values => EmptySegmentInputs<TPosition>.Values;
        public IList<ControlHandle<TPosition>> Handles => EmptySegmentInputs<TPosition>.Handles;

        // Control
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
        public Route() : this(new PathfinderAdapter<TPosition>(new DijkstraPathfinder())) { }
        public Route(IPathfinder<TPosition> pathfinder) : base(Bind(pathfinder)) { }

        private static FindPath Bind(IPathfinder<TPosition> pathfinder)
        {
            if (pathfinder == null) throw new ArgumentNullException(nameof(pathfinder));
            return (INode from, INode to, out Navigation.Path path) =>
            {
                var success = pathfinder.TryPathfind(
                    (IReadOnlyNode<TPosition>)from, new[] { (IReadOnlyNode<TPosition>)to }, out var typed);
                path = typed;
                return success;
            };
        }

        protected override void ValidateNode(INode node)
        {
            if (node is not INode<TPosition>)
                throw new ArgumentException("The node must use the route's position type.", nameof(node));
            base.ValidateNode(node);
        }

        protected override void ValidateSource(ISegment source)
        {
            if (source is not ISegment<TPosition, TRotation>)
                throw new ArgumentException(
                    "The segment must use the route's position and rotation types.", nameof(source));
            base.ValidateSource(source);
        }

        public bool TryGetPosition(Location location, out TPosition position, float? epsilon = null)
        {
            position = default;
            var tolerance = epsilon ?? Epsilon;
            return TryGetTerminalSegment(location, out var segment, out var local, tolerance)
                && ((IReadOnlySegment<TPosition>)segment).TryGetPosition(local, out position, tolerance);
        }

        public bool TryGetLocation(
            TPosition position,
            out Location location,
            bool isForward = true,
            DistanceType distanceType = DistanceType.Length,
            float? epsilon = null) =>
            TryGetLocation(position, 0f, Location.IsFinite(Length) ? Length : 0f,
                out location, out _, isForward, distanceType, epsilon);

        public bool TryGetLocation(
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

        public bool TryGetTransform(
            Location location, out TPosition position, out TRotation rotation, float? epsilon = null)
        {
            position = default;
            rotation = default;
            var tolerance = epsilon ?? Epsilon;
            return TryGetTerminalSegment(location, out var segment, out var local, tolerance)
                && ((IReadOnlySegment<TPosition, TRotation>)segment)
                    .TryGetTransform(local, out position, out rotation, tolerance);
        }
    }
}
