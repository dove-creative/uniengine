using System.Collections.Generic;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;

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
        bool TryGetTerminalSegment(Location location, out IReadOnlySegment segment, out Location terminalLocation,
            float? epsilon = null) =>
            TryGetTerminalSegment(location, -1, out segment, out terminalLocation, out _, epsilon);
        bool TryGetTerminalSegment(Location location, int hintIndex, out IReadOnlySegment segment,
            out Location terminalLocation, out int index, float? epsilon = null) =>
            TerminalPath.For(this).TryResolve(
                location, hintIndex, out segment, out terminalLocation, out index, epsilon);
        bool TryConnectNode(ConnectionSide side, INavNode node);
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
