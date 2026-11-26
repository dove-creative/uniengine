using System.Collections.Generic;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public interface IReadOnlyGeoGroup<TPosition> : IReadOnlyNavGroup
    {
        new IReadOnlyList<IReadOnlyNode<TPosition>> Nodes { get; }
        new IReadOnlyList<IReadOnlySegment<TPosition>> Segments { get; }
        new IReadOnlyList<IReadOnlyGeoGroup<TPosition>> NestedGroups { get; }
        IReadOnlyList<IReadOnlyNavNode> IReadOnlyNavGroup.Nodes => Nodes;
        IReadOnlyList<IReadOnlyNavSegment> IReadOnlyNavGroup.Segments => Segments;
        IReadOnlyList<IReadOnlyNavGroup> IReadOnlyNavGroup.NestedGroups => NestedGroups;
    }

    public interface IReadOnlyGeoGroup<TPosition, TRotation> : IReadOnlyGeoGroup<TPosition>
    {
        new IReadOnlyList<IReadOnlySegment<TPosition, TRotation>> Segments { get; }
        new IReadOnlyList<IReadOnlyGeoGroup<TPosition, TRotation>> NestedGroups { get; }
        IReadOnlyList<IReadOnlySegment<TPosition>> IReadOnlyGeoGroup<TPosition>.Segments => Segments;
        IReadOnlyList<IReadOnlyGeoGroup<TPosition>> IReadOnlyGeoGroup<TPosition>.NestedGroups => NestedGroups;
    }

    public interface IGeoGroup<TPosition, TRotation> : IReadOnlyGeoGroup<TPosition, TRotation>
    {
        new IReadOnlyList<INode<TPosition>> Nodes { get; }
        new IReadOnlyList<ISegment<TPosition, TRotation>> Segments { get; }
        IReadOnlyList<IReadOnlyNode<TPosition>> IReadOnlyGeoGroup<TPosition>.Nodes => Nodes;
        IReadOnlyList<IReadOnlySegment<TPosition, TRotation>>
            IReadOnlyGeoGroup<TPosition, TRotation>.Segments => Segments;

        bool AddNode(INode<TPosition> node);
        bool RemoveNode(INode<TPosition> node);
        bool AddSegment(ISegment<TPosition, TRotation> segment);
        bool RemoveSegment(ISegment<TPosition, TRotation> segment);
        bool AddGroup(IReadOnlyGeoGroup<TPosition, TRotation> group);
        bool RemoveGroup(IReadOnlyGeoGroup<TPosition, TRotation> group);
        void Clear();
    }
}
