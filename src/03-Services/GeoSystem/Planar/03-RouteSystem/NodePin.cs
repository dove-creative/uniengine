using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using INode = BlackThunder.GeoSystem.Geometry.Planar.INode;
using ISegment = BlackThunder.GeoSystem.Geometry.Planar.ISegment;
using IReadOnlyNode = BlackThunder.GeoSystem.Geometry.Planar.IReadOnlyNode;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class NodePin : RouteSystem.NodePin<Vector2>, INode, IPin
    {
        // Front
        public new IEnumerable<ISegment> Segments => base.Segments.Cast<ISegment>();
        public new IReadOnlyNode AnchorNode => (IReadOnlyNode)base.AnchorNode;


        // Content
        public NodePin() { }
        public NodePin(IReadOnlyNode anchor) : base(anchor) { }

        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment)
                throw new ArgumentException("A planar node requires a planar segment.", nameof(segment));
            base.OnSegmentConnecting(segment);
        }

        protected override void ValidateAnchor(Geometry.IReadOnlyNode<Vector2> anchor)
        {
            if (anchor is not IReadOnlyNode)
                throw new ArgumentException("A planar anchor is required.", nameof(anchor));
            base.ValidateAnchor(anchor);
        }
    }
}
