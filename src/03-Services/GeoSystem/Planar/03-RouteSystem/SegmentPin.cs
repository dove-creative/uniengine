using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using INode = BlackThunder.GeoSystem.Geometry.Planar.INode;
using ISegment = BlackThunder.GeoSystem.Geometry.Planar.ISegment;
using IReadOnlySegment = BlackThunder.GeoSystem.Geometry.Planar.IReadOnlySegment;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class SegmentPin : RouteSystem.SegmentPin<Vector2, float>, INode, IPin
    {
        // Front
        public new IEnumerable<ISegment> Segments => base.Segments.Cast<ISegment>();
        public new IReadOnlySegment AnchorSegment => (IReadOnlySegment)base.AnchorSegment;


        // Content
        public SegmentPin() { }
        public SegmentPin(IReadOnlySegment anchor, Location location = default) : base(anchor, location) { }

        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment)
                throw new ArgumentException("A planar node requires a planar segment.", nameof(segment));
            base.OnSegmentConnecting(segment);
        }

        protected override void ValidateAnchor(Geometry.IReadOnlySegment<Vector2, float> anchor)
        {
            if (anchor is not IReadOnlySegment)
                throw new ArgumentException("A planar anchor is required.", nameof(anchor));
            base.ValidateAnchor(anchor);
        }
    }
}
