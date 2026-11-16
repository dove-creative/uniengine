using System.Collections.Generic;
using BlackThunder.GeoSystem.Geometry;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public interface ISegmentView
    {
        ISegment Source { get; }
        bool IsForward { get; }
    }

    public interface IRoute : ISegment
    {
        IReadOnlyList<ISegmentView> Views { get; }
        void Append(ISegmentView view, bool toTail = true);
        void Remove(ISegment segment);
        void Clear();
    }

    public interface IClip : ISegment
    {
        new ISegmentPin From { get; }
        new ISegmentPin To { get; }
        IReadOnlySegment SegmentSource { get; }
        bool IsForward { get; }
        bool IsValid { get; }
        void Clear();
    }
}
