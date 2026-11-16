using System.Collections.Generic;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public interface IReadOnlySegment : IReadOnlyNavSegment
    {
        new IReadOnlyNode From { get; }
        new IReadOnlyNode To { get; }
        new IEnumerable<IReadOnlyNode> Nodes { get; }
        IReadOnlyNavNode IReadOnlyNavSegment.From => From;
        IReadOnlyNavNode IReadOnlyNavSegment.To => To;
        IEnumerable<IReadOnlyNavNode> IReadOnlyNavSegment.Nodes => Nodes;
    }

    public interface ISegment : IReadOnlySegment, INavSegment
    {
        new INode From { get; }
        new INode To { get; }
        new IEnumerable<INode> Nodes { get; }
        IReadOnlyNode IReadOnlySegment.From => From;
        IReadOnlyNode IReadOnlySegment.To => To;
        IEnumerable<IReadOnlyNode> IReadOnlySegment.Nodes => Nodes;
        INavNode INavSegment.From => From;
        INavNode INavSegment.To => To;
        IEnumerable<INavNode> INavSegment.Nodes => Nodes;
        IReadOnlyNavNode IReadOnlyNavSegment.From => From;
        IReadOnlyNavNode IReadOnlyNavSegment.To => To;
        IEnumerable<IReadOnlyNavNode> IReadOnlyNavSegment.Nodes => Nodes;
    }

    public interface ILinearSegment : ISegment { }
}
