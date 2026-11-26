using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Objects;
using INode = BlackThunder.GeoSystem.Geometry.Planar.INode;
using ISegment = BlackThunder.GeoSystem.Geometry.Planar.ISegment;
using IReadOnlySegment = BlackThunder.GeoSystem.Geometry.Planar.IReadOnlySegment;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class Clip : RouteSystem.Clip<Vector2, float>, ISegment
    {
        // Front
        public new SegmentPin From => (SegmentPin)base.From;
        public new SegmentPin To => (SegmentPin)base.To;
        public new IReadOnlySegment SegmentSource => (IReadOnlySegment)base.SegmentSource;
        public new IEnumerable<INode> Nodes => base.Nodes.Cast<INode>();
        INode ISegment.From => From;
        INode ISegment.To => To;
        Geometry.IReadOnlyNode<Vector2> Geometry.IReadOnlySegment<Vector2>.From => From;
        Geometry.IReadOnlyNode<Vector2> Geometry.IReadOnlySegment<Vector2>.To => To;
        IEnumerable<Geometry.IReadOnlyNode<Vector2>> Geometry.IReadOnlySegment<Vector2>.Nodes => Nodes;


        // Content
        public Clip() { }
        public Clip(SegmentPin from, SegmentPin to) : base(from, to) { }

        protected override void OnNodeConnecting(ConnectionSide side, INavNode node)
        {
            if (node is not SegmentPin)
                throw new ArgumentException("A planar clip requires a planar segment pin.", nameof(node));
            base.OnNodeConnecting(side, node);
        }

        public bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null)
        {
            var success = base.TryGetTransform(location, out var position, out var rotation, epsilon);
            transform = success ? new Transform2D(position, rotation) : default;
            return success;
        }
    }
}
