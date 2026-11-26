using System.Collections.Generic;
using BlackThunder.UniEngine.Core;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.Geometry;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public class RouteGroup<TPosition, TRotation> : IRouteGroup<TPosition, TRotation>
    {
        // Front
        public IReadOnlyList<Hold<TPosition, TRotation>> Holds => _holds.Items;
        public IReadOnlyList<Route<TPosition, TRotation>> Routes => _routes.Items;
        public IReadOnlyList<IReadOnlyRouteGroup<TPosition, TRotation>> NestedGroups => _nestedGroups.Items;

        // Property
        public bool IsDisposed => _state.IsDisposed;
        public ILifecycleHub LifecycleHub => _state.Hub;

        // Internal
        private readonly GroupState _state = new();
        private readonly GroupMembers<Hold<TPosition, TRotation>> _holds;
        private readonly GroupMembers<Route<TPosition, TRotation>> _routes;
        private readonly GroupMembers<IReadOnlyRouteGroup<TPosition, TRotation>> _nestedGroups;


        // Content
        public RouteGroup()
        {
            _holds = new GroupMembers<Hold<TPosition, TRotation>>(_state);
            _routes = new GroupMembers<Route<TPosition, TRotation>>(_state);
            _nestedGroups = new GroupMembers<IReadOnlyRouteGroup<TPosition, TRotation>>(_state);
        }

        public bool AddHold(Hold<TPosition, TRotation> hold) => _holds.Add(hold);
        public bool RemoveHold(Hold<TPosition, TRotation> hold) => _holds.Remove(hold);

        public bool AddRoute(Route<TPosition, TRotation> route) => _routes.Add(route);
        public bool RemoveRoute(Route<TPosition, TRotation> route) => _routes.Remove(route);

        public bool AddGroup(IReadOnlyRouteGroup<TPosition, TRotation> group)
        {
            _state.ThrowIfDisposed();
            GroupHierarchy.Validate(this, group);
            return _nestedGroups.Add(group);
        }
        public bool RemoveGroup(IReadOnlyRouteGroup<TPosition, TRotation> group) => _nestedGroups.Remove(group);

        public void Clear() => _state.Clear();
        public void Dispose() => _state.Dispose();
    }
}
