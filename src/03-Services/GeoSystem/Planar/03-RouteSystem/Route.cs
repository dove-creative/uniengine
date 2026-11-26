using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Objects;
using INode = BlackThunder.GeoSystem.Geometry.Planar.INode;
using ISegment = BlackThunder.GeoSystem.Geometry.Planar.ISegment;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class Route : RouteSystem.Route<Vector2, float>, ISegment
    {
        // Front
        public new INode From => (INode)base.From;
        public new INode To => (INode)base.To;
        public new IEnumerable<INode> Nodes => base.Nodes.Cast<INode>();
        Geometry.IReadOnlyNode<Vector2> Geometry.IReadOnlySegment<Vector2>.From => From;
        Geometry.IReadOnlyNode<Vector2> Geometry.IReadOnlySegment<Vector2>.To => To;
        IEnumerable<Geometry.IReadOnlyNode<Vector2>> Geometry.IReadOnlySegment<Vector2>.Nodes => Nodes;


        // Content
        public Route() : this(new AStarPathfinder<Vector2>(Vector2.Distance)) { }
        public Route(IPathfinder<Vector2> pathfinder) : base(pathfinder) { }

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

        public bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null)
        {
            var success = base.TryGetTransform(location, out var position, out var rotation, epsilon);
            transform = success ? new Transform2D(position, rotation) : default;
            return success;
        }
    }
}
