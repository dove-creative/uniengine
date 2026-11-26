using System.Collections.Generic;
using BlackThunder.UniEngine.Core;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public class GeoGroup<TPosition, TRotation> : IGeoGroup<TPosition, TRotation>
    {
        // Front
        public IReadOnlyList<INode<TPosition>> Nodes => _nodes.Items;
        public IReadOnlyList<ISegment<TPosition, TRotation>> Segments => _segments.Items;
        public IReadOnlyList<IReadOnlyGeoGroup<TPosition, TRotation>> NestedGroups => _nestedGroups.Items;

        // Property
        public bool IsDisposed => _state.IsDisposed;
        public ILifecycleHub LifecycleHub => _state.Hub;

        // Internal
        private readonly GroupState _state = new();
        private readonly GroupMembers<INode<TPosition>> _nodes;
        private readonly GroupMembers<ISegment<TPosition, TRotation>> _segments;
        private readonly GroupMembers<IReadOnlyGeoGroup<TPosition, TRotation>> _nestedGroups;


        // Content
        public GeoGroup()
        {
            _nodes = new GroupMembers<INode<TPosition>>(_state);
            _segments = new GroupMembers<ISegment<TPosition, TRotation>>(_state);
            _nestedGroups = new GroupMembers<IReadOnlyGeoGroup<TPosition, TRotation>>(_state);
        }

        public bool AddNode(INode<TPosition> node) => _nodes.Add(node);
        public bool RemoveNode(INode<TPosition> node) => _nodes.Remove(node);

        public bool AddSegment(ISegment<TPosition, TRotation> segment) => _segments.Add(segment);
        public bool RemoveSegment(ISegment<TPosition, TRotation> segment) => _segments.Remove(segment);

        public bool AddGroup(IReadOnlyGeoGroup<TPosition, TRotation> group)
        {
            _state.ThrowIfDisposed();
            GroupHierarchy.Validate(this, group);
            return _nestedGroups.Add(group);
        }
        public bool RemoveGroup(IReadOnlyGeoGroup<TPosition, TRotation> group) => _nestedGroups.Remove(group);

        public void Clear() => _state.Clear();
        public void Dispose() => _state.Dispose();
    }
}
