using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public static class GeoGroupExtensions
    {
        public static IEnumerable<IReadOnlyNode<TPosition>> EnumerateNodes<TPosition>(
            this IReadOnlyGeoGroup<TPosition> group) =>
            GroupHierarchy.Members<IReadOnlyGeoGroup<TPosition>, IReadOnlyNode<TPosition>>(
                group, current => current.NestedGroups, current => current.Nodes);

        public static IEnumerable<IReadOnlySegment<TPosition>> EnumerateSegments<TPosition>(
            this IReadOnlyGeoGroup<TPosition> group) =>
            GroupHierarchy.Members<IReadOnlyGeoGroup<TPosition>, IReadOnlySegment<TPosition>>(
                group, current => current.NestedGroups, current => current.Segments);

        public static IEnumerable<IReadOnlyGeoGroup<TPosition>> EnumerateDescendants<TPosition>(
            this IReadOnlyGeoGroup<TPosition> group) =>
            GroupHierarchy.Traverse<IReadOnlyGeoGroup<TPosition>>(
                group, current => current.NestedGroups).Skip(1);

        public static IEnumerable<IReadOnlyNode<TPosition>> EnumerateNodes<TPosition, TRotation>(
            this IReadOnlyGeoGroup<TPosition, TRotation> group) =>
            GroupHierarchy.Members<IReadOnlyGeoGroup<TPosition, TRotation>, IReadOnlyNode<TPosition>>(
                group, current => current.NestedGroups, current => current.Nodes);

        public static IEnumerable<IReadOnlySegment<TPosition, TRotation>> EnumerateSegments<TPosition, TRotation>(
            this IReadOnlyGeoGroup<TPosition, TRotation> group) =>
            GroupHierarchy.Members<IReadOnlyGeoGroup<TPosition, TRotation>, IReadOnlySegment<TPosition, TRotation>>(
                group, current => current.NestedGroups, current => current.Segments);

        public static IEnumerable<IReadOnlyGeoGroup<TPosition, TRotation>> EnumerateDescendants<TPosition, TRotation>(
            this IReadOnlyGeoGroup<TPosition, TRotation> group) =>
            GroupHierarchy.Traverse<IReadOnlyGeoGroup<TPosition, TRotation>>(
                group, current => current.NestedGroups).Skip(1);
    }
}
