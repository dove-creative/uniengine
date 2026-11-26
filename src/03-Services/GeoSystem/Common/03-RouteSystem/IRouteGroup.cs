using System.Collections.Generic;
using BlackThunder.GeoSystem.Geometry;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public interface IReadOnlyRouteGroup<TPosition, TRotation> : IReadOnlyGeoGroup<TPosition, TRotation>
    {
        IReadOnlyList<Hold<TPosition, TRotation>> Holds { get; }
        IReadOnlyList<Route<TPosition, TRotation>> Routes { get; }
        new IReadOnlyList<IReadOnlyRouteGroup<TPosition, TRotation>> NestedGroups { get; }
        IReadOnlyList<IReadOnlyNode<TPosition>> IReadOnlyGeoGroup<TPosition>.Nodes => Holds;
        IReadOnlyList<IReadOnlySegment<TPosition, TRotation>>
            IReadOnlyGeoGroup<TPosition, TRotation>.Segments => Routes;
        IReadOnlyList<IReadOnlyGeoGroup<TPosition, TRotation>>
            IReadOnlyGeoGroup<TPosition, TRotation>.NestedGroups => NestedGroups;
    }

    public interface IRouteGroup<TPosition, TRotation> : IReadOnlyRouteGroup<TPosition, TRotation>
    {
        bool AddHold(Hold<TPosition, TRotation> hold);
        bool RemoveHold(Hold<TPosition, TRotation> hold);
        bool AddRoute(Route<TPosition, TRotation> route);
        bool RemoveRoute(Route<TPosition, TRotation> route);
        bool AddGroup(IReadOnlyRouteGroup<TPosition, TRotation> group);
        bool RemoveGroup(IReadOnlyRouteGroup<TPosition, TRotation> group);
        void Clear();
    }
}
