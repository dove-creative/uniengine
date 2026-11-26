using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using INode = BlackThunder.GeoSystem.Geometry.Planar.INode;
using ISegment = BlackThunder.GeoSystem.Geometry.Planar.ISegment;
using IReadOnlyNode = BlackThunder.GeoSystem.Geometry.Planar.IReadOnlyNode;
using IReadOnlySegment = BlackThunder.GeoSystem.Geometry.Planar.IReadOnlySegment;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class Hold : RouteSystem.Hold<Vector2, float>, INode, IPin
    {
        // Front
        public new IEnumerable<ISegment> Segments => base.Segments.Cast<ISegment>();
        public new IPin Anchor => (IPin)base.Anchor;


        // Content
        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment)
                throw new ArgumentException("A planar node requires a planar segment.", nameof(segment));
            base.OnSegmentConnecting(segment);
        }

        protected override void ValidateNode(Geometry.INode<Vector2> node)
        {
            if (node is not INode)
                throw new ArgumentException("A planar node is required.", nameof(node));
            base.ValidateNode(node);
        }

        protected override void ValidateSegment(Geometry.ISegment<Vector2, float> segment)
        {
            if (segment is not ISegment)
                throw new ArgumentException("A planar segment is required.", nameof(segment));
            base.ValidateSegment(segment);
        }

        protected override RouteSystem.NodePin<Vector2> CreateNodePin(Geometry.IReadOnlyNode<Vector2> node) =>
            new NodePin((IReadOnlyNode)node);
        protected override RouteSystem.SegmentPin<Vector2, float> CreateSegmentPin(
            Geometry.IReadOnlySegment<Vector2, float> segment, Location location) =>
            new SegmentPin((IReadOnlySegment)segment, location);
    }
}
