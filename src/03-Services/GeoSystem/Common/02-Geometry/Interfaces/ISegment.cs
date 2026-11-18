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
        INavNode INavSegment.From => From;
        INavNode INavSegment.To => To;
        IEnumerable<INavNode> INavSegment.Nodes => Nodes;
    }

    public interface IReadOnlySegment<TPosition> : IReadOnlySegment
    {
        new IReadOnlyNode<TPosition> From { get; }
        new IReadOnlyNode<TPosition> To { get; }
        new IEnumerable<IReadOnlyNode<TPosition>> Nodes { get; }
        IReadOnlyNode IReadOnlySegment.From => From;
        IReadOnlyNode IReadOnlySegment.To => To;
        IEnumerable<IReadOnlyNode> IReadOnlySegment.Nodes => Nodes;

        bool TryGetPosition(Location location, out TPosition position, float? epsilon = null);
    }

    public interface ISegment<TPosition> : IReadOnlySegment<TPosition>, ISegment
    {
        new INode<TPosition> From { get; }
        new INode<TPosition> To { get; }
        new IEnumerable<INode<TPosition>> Nodes { get; }
        INode ISegment.From => From;
        INode ISegment.To => To;
        IEnumerable<INode> ISegment.Nodes => Nodes;
    }

    public interface IReadOnlySegment<TPosition, TRotation> : IReadOnlySegment<TPosition>
    {
        bool TryGetTransform(Location location, out TPosition position, out TRotation rotation, float? epsilon = null);
    }

    public interface ISegment<TPosition, TRotation> : ISegment<TPosition>, IReadOnlySegment<TPosition, TRotation>
    {
        IDictionary<string, float> Values { get; }
        IList<ControlHandle<TPosition>> Handles { get; }
    }
}
