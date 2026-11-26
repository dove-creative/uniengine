using System.Collections.Generic;
using System.Linq;

namespace BlackThunder.GeoSystem.Navigation
{
    public static class NavGroupExtensions
    {
        public static IEnumerable<IReadOnlyNavNode> EnumerateNodes(this IReadOnlyNavGroup group) =>
            GroupHierarchy.Members<IReadOnlyNavGroup, IReadOnlyNavNode>(
                group, current => current.NestedGroups, current => current.Nodes);

        public static IEnumerable<IReadOnlyNavSegment> EnumerateSegments(this IReadOnlyNavGroup group) =>
            GroupHierarchy.Members<IReadOnlyNavGroup, IReadOnlyNavSegment>(
                group, current => current.NestedGroups, current => current.Segments);

        public static IEnumerable<IReadOnlyNavGroup> EnumerateDescendants(this IReadOnlyNavGroup group) =>
            GroupHierarchy.Traverse<IReadOnlyNavGroup>(group, current => current.NestedGroups).Skip(1);
    }
}
