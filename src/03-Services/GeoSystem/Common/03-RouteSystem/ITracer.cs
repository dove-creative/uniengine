using BlackThunder.GeoSystem.Geometry;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public enum LocationType { RouteBased, Absolute }
    public enum TracerState { Derailed, Rolling, Tracking }
    public enum MoveFailureOption { Derailed = 0, TrackingFailure = 1 }

    public interface ITracer<TPosition, TRotation>
    {
        IRoute Route { get; }
        IReadOnlySegment<TPosition, TRotation> Segment { get; }
        Location Location { get; }
        LocationType LocationType { get; }
        bool IsForward { get; set; }
        Wheel Wheel { get; }
        TracerState State { get; }

        bool PlaceOnRoute(IRoute route, Location location = default,
            LocationType locationType = LocationType.RouteBased);
        bool PlaceOnSegment(IReadOnlySegment<TPosition, TRotation> segment, Location location = default);
        void DetachRoute();
        bool TryGetTransform(out TPosition position, out TRotation rotation);
        bool Move(float distance, out TracerState endState, out float remaining);
    }
}
