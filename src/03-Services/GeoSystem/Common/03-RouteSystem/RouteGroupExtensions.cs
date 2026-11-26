using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public static class RouteGroupExtensions
    {
        public static IEnumerable<Hold<TPosition, TRotation>> EnumerateHolds<TPosition, TRotation>(
            this IReadOnlyRouteGroup<TPosition, TRotation> group) =>
            GroupHierarchy.Members<IReadOnlyRouteGroup<TPosition, TRotation>, Hold<TPosition, TRotation>>(
                group, current => current.NestedGroups, current => current.Holds);

        public static IEnumerable<Route<TPosition, TRotation>> EnumerateRoutes<TPosition, TRotation>(
            this IReadOnlyRouteGroup<TPosition, TRotation> group) =>
            GroupHierarchy.Members<IReadOnlyRouteGroup<TPosition, TRotation>, Route<TPosition, TRotation>>(
                group, current => current.NestedGroups, current => current.Routes);

        public static IEnumerable<IReadOnlyRouteGroup<TPosition, TRotation>> EnumerateDescendants<TPosition, TRotation>(
            this IReadOnlyRouteGroup<TPosition, TRotation> group) =>
            GroupHierarchy.Traverse<IReadOnlyRouteGroup<TPosition, TRotation>>(
                group, current => current.NestedGroups).Skip(1);
    }
}
