using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public abstract class PositionedNode : NavNode, INode
    {
        // Front
        public Vector2 Position { get; protected set; }
        public new IEnumerable<ISegment> Segments => base.Segments.Cast<ISegment>();
        IEnumerable<Geometry.ISegment> Geometry.INode.Segments => Segments;


        // Content
        protected override void OnSegmentConnecting(INavSegment segment)
        {
            if (segment is not ISegment)
                throw new ArgumentException("A planar node requires a planar segment.", nameof(segment));
        }
    }
}
