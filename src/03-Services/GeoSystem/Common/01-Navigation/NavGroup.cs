using System.Collections.Generic;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.GeoSystem.Navigation
{
    public class NavGroup : INavGroup
    {
        // Front
        public IReadOnlyList<INavNode> Nodes => _nodes.Items;
        public IReadOnlyList<INavSegment> Segments => _segments.Items;
        public IReadOnlyList<IReadOnlyNavGroup> NestedGroups => _nestedGroups.Items;

        // Property
        public bool IsDisposed => _state.IsDisposed;
        public ILifecycleHub LifecycleHub => _state.Hub;

        // Internal
        private readonly GroupState _state = new();
        private readonly GroupMembers<INavNode> _nodes;
        private readonly GroupMembers<INavSegment> _segments;
        private readonly GroupMembers<IReadOnlyNavGroup> _nestedGroups;


        // Content
        public NavGroup()
        {
            _nodes = new GroupMembers<INavNode>(_state);
            _segments = new GroupMembers<INavSegment>(_state);
            _nestedGroups = new GroupMembers<IReadOnlyNavGroup>(_state);
        }

        public bool AddNode(INavNode node) => _nodes.Add(node);
        public bool RemoveNode(INavNode node) => _nodes.Remove(node);

        public bool AddSegment(INavSegment segment) => _segments.Add(segment);
        public bool RemoveSegment(INavSegment segment) => _segments.Remove(segment);

        public bool AddGroup(IReadOnlyNavGroup group)
        {
            _state.ThrowIfDisposed();
            GroupHierarchy.Validate(this, group);
            return _nestedGroups.Add(group);
        }
        public bool RemoveGroup(IReadOnlyNavGroup group) => _nestedGroups.Remove(group);

        public void Clear() => _state.Clear();
        public void Dispose() => _state.Dispose();
    }
}
