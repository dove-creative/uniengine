using System.Collections.Generic;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.GeoSystem.Navigation
{
    public interface IReadOnlyNavGroup : ILifecycleSubject
    {
        IReadOnlyList<IReadOnlyNavNode> Nodes { get; }
        IReadOnlyList<IReadOnlyNavSegment> Segments { get; }
        IReadOnlyList<IReadOnlyNavGroup> NestedGroups { get; }
    }

    public interface INavGroup : IReadOnlyNavGroup
    {
        new IReadOnlyList<INavNode> Nodes { get; }
        new IReadOnlyList<INavSegment> Segments { get; }
        IReadOnlyList<IReadOnlyNavNode> IReadOnlyNavGroup.Nodes => Nodes;
        IReadOnlyList<IReadOnlyNavSegment> IReadOnlyNavGroup.Segments => Segments;

        bool AddNode(INavNode node);
        bool RemoveNode(INavNode node);
        bool AddSegment(INavSegment segment);
        bool RemoveSegment(INavSegment segment);
        bool AddGroup(IReadOnlyNavGroup group);
        bool RemoveGroup(IReadOnlyNavGroup group);
        void Clear();
    }
}
