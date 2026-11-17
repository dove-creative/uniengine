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

    public interface IReadOnlySegment<P> : IReadOnlySegment
    {
        new IReadOnlyNode<P> From { get; }
        new IReadOnlyNode<P> To { get; }
        new IEnumerable<IReadOnlyNode<P>> Nodes { get; }
        IReadOnlyNode IReadOnlySegment.From => From;
        IReadOnlyNode IReadOnlySegment.To => To;
        IEnumerable<IReadOnlyNode> IReadOnlySegment.Nodes => Nodes;
        IReadOnlyNavNode IReadOnlyNavSegment.From => From;
        IReadOnlyNavNode IReadOnlyNavSegment.To => To;
        IEnumerable<IReadOnlyNavNode> IReadOnlyNavSegment.Nodes => Nodes;

        bool TryGetPosition(Location location, out P position, float? epsilon = null);
    }

    public interface ISegment<P> : IReadOnlySegment<P>, ISegment
    {
        new INode<P> From { get; }
        new INode<P> To { get; }
        new IEnumerable<INode<P>> Nodes { get; }
        IReadOnlyNode<P> IReadOnlySegment<P>.From => From;
        IReadOnlyNode<P> IReadOnlySegment<P>.To => To;
        IEnumerable<IReadOnlyNode<P>> IReadOnlySegment<P>.Nodes => Nodes;
        INode ISegment.From => From;
        INode ISegment.To => To;
        IEnumerable<INode> ISegment.Nodes => Nodes;
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

    public interface IReadOnlySegment<P, R> : IReadOnlySegment<P>
    {
        bool TryGetTransform(Location location, out P position, out R rotation, float? epsilon = null);
    }

    public interface ISegment<P, R> : ISegment<P>, IReadOnlySegment<P, R>
    {
        IDictionary<string, float> Values { get; }
        IList<SegmentHandle<P>> Handles { get; }
    }
}
