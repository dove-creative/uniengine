using System;
using BlackThunder.GeoSystem.Geometry.Planar;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public readonly struct SegmentView : ISegmentView
    {
        public ISegment Source { get; }
        public bool IsForward { get; }
        Geometry.ISegment ISegmentView.Source => Source;


        public SegmentView(ISegment source, bool isForward = true)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            IsForward = isForward;
        }

        public SegmentView(ISegment source, INode from)
            : this(source, source?.IsFromNode(from) ?? throw new ArgumentNullException(nameof(source))) { }
    }
}
