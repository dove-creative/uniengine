using System.Collections.Generic;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public interface IReadOnlyNode : IReadOnlyNavNode
    {
        new IEnumerable<IReadOnlySegment> Segments { get; }
        IEnumerable<IReadOnlyNavSegment> IReadOnlyNavNode.Segments => Segments;
    }

    public interface INode : IReadOnlyNode, INavNode
    {
        new IEnumerable<ISegment> Segments { get; }
        IEnumerable<IReadOnlySegment> IReadOnlyNode.Segments => Segments;
        IEnumerable<INavSegment> INavNode.Segments => Segments;
        IEnumerable<IReadOnlyNavSegment> IReadOnlyNavNode.Segments => Segments;
    }
}
