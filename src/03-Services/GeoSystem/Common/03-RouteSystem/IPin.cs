using BlackThunder.UniEngine.Core;
using BlackThunder.GeoSystem.Geometry;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public interface IPin : IEventSubject<int>
    {
        bool IsValid { get; }
    }

    public interface IPin<TPosition> : IPin, IPositioned<TPosition> { }

    public interface INodePin : IPin, INode
    {
        IReadOnlyNode AnchorNode { get; }
        void Connect(IReadOnlyNode anchor);
        void Disconnect();
    }

    public interface ISegmentPin : IPin, INode
    {
        IReadOnlySegment AnchorSegment { get; }
        Location Location { get; set; }
        void Connect(IReadOnlySegment anchor, Location? location = null);
        void Disconnect();
    }

    public interface IHold : IPin, INode
    {
        IPin Anchor { get; }
        void MoveTo(INode node);
        void MoveTo(ISegment segment, Location location);
    }
}
