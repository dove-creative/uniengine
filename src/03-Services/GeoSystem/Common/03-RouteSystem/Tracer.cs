using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.GeoSystem.Geometry;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public class Tracer<TPosition, TRotation> : ITracer<TPosition, TRotation>
    {
        // Front
        public IRoute Route { get { Refresh(); return _route; } }
        public IReadOnlySegment<TPosition, TRotation> Segment { get { Refresh(); return _segment; } }
        public Location Location
        {
            get
            {
                Refresh();
                return LocationType == LocationType.Absolute ? _local : _routeLocation;
            }
        }
        public LocationType LocationType { get; private set; }
        public Wheel Wheel { get; } = new();
        public TracerState State => Refresh();

        public bool IsForward
        {
            get { Refresh(); return _route != null ? _routeLocation.IsForward : _local.IsForward; }
            set
            {
                if (IsForward == value) return;
                if (PathSource != null) _routeLocation = Reverse(_routeLocation, PathSource.Length);
                _local = Reverse(_local, _segment?.Length ?? 0f);
            }
        }

        // Internal
        private readonly TracerStateMachine _machine = new();
        private readonly Func<TRotation, TRotation, float> _turnAngle;
        private IRoute _route;
        private IReadOnlySegment<TPosition, TRotation> _rollingEdge;
        private IReadOnlySegment PathSource => (IReadOnlySegment)_route ?? _rollingEdge;
        private IReadOnlySegment<TPosition, TRotation> _segment;
        private Location _routeLocation;
        private Location _local;
        private TerminalSpan _occurrence;
        private int _index = -1;

        private readonly struct Exit : IEquatable<Exit>
        {
            private readonly IReadOnlySegment _edge;
            private readonly bool _forward;
            private readonly int _reservations;

            public Exit(IReadOnlySegment edge, bool forward, int reservations)
            {
                _edge = edge;
                _forward = forward;
                _reservations = reservations;
            }

            public bool Equals(Exit other) => ReferenceEquals(_edge, other._edge)
                && _forward == other._forward && _reservations == other._reservations;
            public override bool Equals(object obj) => obj is Exit other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_edge), _forward, _reservations);
        }


        // Content --------------------
        // Placement
        public Tracer(Func<TRotation, TRotation, float> turnAngle = null) => _turnAngle = turnAngle;

        public bool PlaceOnRoute(IRoute route, Location location = default,
            LocationType locationType = LocationType.RouteBased)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (locationType != LocationType.RouteBased && locationType != LocationType.Absolute)
                throw new ArgumentOutOfRangeException(nameof(locationType));
            var path = TerminalPath.For(route);
            if (!TryLocate(path, location, out var segment, out var local, out var index)) return false;
            _route = route;
            _rollingEdge = null;
            _routeLocation = location;
            LocationType = locationType;
            SetTerminal(path, segment, local, index);
            return true;
        }

        public bool PlaceOnSegment(IReadOnlySegment<TPosition, TRotation> segment, Location location = default)
        {
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            if (segment is IRoute || segment is IClip)
                throw new ArgumentException("A terminal segment is required.", nameof(segment));
            if (!HasPosition(segment, location, null)) return false;
            LeaveRoute();
            LocationType = LocationType.Absolute;
            _segment = segment;
            _local = location;
            return true;
        }

        private bool TryLocate(TerminalPath path, Location location, out IReadOnlySegment<TPosition, TRotation> segment,
            out Location local, out int index)
        {
            segment = null;
            if (!path.TryResolve(location, _index, out var source, out local, out index)
                || source is not IReadOnlySegment<TPosition, TRotation> typed
                || !HasPosition(typed, local, path.Epsilon)) return false;
            segment = typed;
            return true;
        }

        private void SetTerminal(TerminalPath path, IReadOnlySegment<TPosition, TRotation> segment,
            Location local, int index)
        {
            _segment = segment;
            _local = local;
            _index = index;
            _occurrence = path[index];
        }

        private void LeaveRoute()
        {
            _route = null;
            _rollingEdge = null;
            _occurrence = null;
            _index = -1;
        }

        public void DetachRoute()
        {
            if (_route == null) return;
            Refresh();
            LeaveRoute();
        }

        // State
        private TracerState Refresh()
        {
            if (PathSource?.IsDisposed == true) LeaveRoute();
            if (LocationType == LocationType.RouteBased)
            {
                _segment = null;
                if (_route != null)
                {
                    var path = TerminalPath.For(_route);
                    if (TryLocate(path, _routeLocation, out var segment, out var local, out var index))
                        SetTerminal(path, segment, local, index);
                }
            }
            else if (_segment != null)
            {
                if (_segment.IsDisposed || !HasPosition(_segment, _local,
                    PathSource == null ? null : TerminalPath.For(PathSource).Epsilon))
                {
                    _segment = null;
                    LeaveRoute();
                }
                else if (PathSource != null)
                {
                    var path = TerminalPath.For(PathSource);
                    _index = path.FindOccurrence(_occurrence, _index);
                    if (_index < 0 || !path[_index].TryToRoute(
                        _local, path.Epsilon, out _routeLocation, PathSource.Length))
                        LeaveRoute();
                    else _occurrence = path[_index];
                }
            }
            return _machine.Select(_segment != null, _route != null);
        }

        public bool TryGetTransform(out TPosition position, out TRotation rotation)
        {
            if (Refresh() != TracerState.Derailed)
                return ReadTransform(_segment, _local, out position, out rotation,
                    PathSource == null ? null : TerminalPath.For(PathSource).Epsilon);
            position = default;
            rotation = default;
            return false;
        }

        protected virtual bool HasPosition(IReadOnlySegment<TPosition, TRotation> segment, Location location,
            float? epsilon) => segment.TryGetPosition(location, out _, epsilon);

        protected virtual bool ReadTransform(IReadOnlySegment<TPosition, TRotation> segment, Location location,
            out TPosition position, out TRotation rotation, float? epsilon) =>
            segment.TryGetTransform(location, out position, out rotation, epsilon);

        // Movement
        public bool Move(float distance, out TracerState endState, out float remaining)
        {
            if (!Location.IsFinite(distance) || distance < 0f) throw new ArgumentOutOfRangeException(nameof(distance));
            remaining = distance;
            endState = Refresh();
            if (endState == TracerState.Derailed) return false;
            if (endState == TracerState.Tracking && distance > 0f)
            {
                AdvanceOnPath(distance, out var advanced);
                remaining = (float)(distance - advanced);
                endState = Refresh();
            }
            if (endState == TracerState.Rolling)
            {
                if (Wheel.MoveFailureOption == MoveFailureOption.TrackingFailure) return false;
                Roll(ref remaining);
                endState = Refresh();
            }
            return endState != TracerState.Derailed;
        }

        private bool AdvanceOnPath(double requested, out double advanced)
        {
            var source = PathSource;
            var path = TerminalPath.For(source);
            var length = source.Length;
            var traveled = DirectedDistance(_routeLocation, length);
            var step = Math.Min(requested, length - traveled);
            var target = AtDistance(_routeLocation, traveled + step, length);
            advanced = 0d;
            if (step == 0d) return true;
            if (!path.TryResolve(target, _index, out _, out var destination, out var last))
            {
                LeaveRoute();
                return false;
            }

            var forward = target.IsForward;
            var direction = last >= _index ? 1 : -1;
            for (var i = _index; ; i += direction)
            {
                var span = path[i];
                var boundary = forward ? span.End : span.Start;
                var local = i == last ? destination : span.ToTerminal(boundary, forward);
                if (span.Segment is not IReadOnlySegment<TPosition, TRotation> segment
                    || !HasPosition(segment, local, path.Epsilon))
                {
                    LeaveRoute();
                    return false;
                }
                SetTerminal(path, segment, local, i);
                if (i == last)
                {
                    _routeLocation = target;
                    advanced = step;
                    return true;
                }
                var directedBoundary = Math.Clamp(forward ? boundary : length - boundary, traveled, traveled + step);
                _routeLocation = AtDistance(target, directedBoundary, length);
                advanced = directedBoundary - traveled;
            }
        }

        private void Roll(ref float remaining)
        {
            double requested = remaining, traveled = 0d;
            Dictionary<Exit, double> visits = null;
            while (requested > traveled)
            {
                var edge = _rollingEdge ?? _segment;
                var location = _rollingEdge == null ? _local : _routeLocation;
                var length = edge.Length;
                var distance = DirectedDistance(location, length);
                var available = length - distance;
                var pending = requested - traveled;
                if (_rollingEdge != null)
                {
                    if (!AdvanceOnPath(pending, out var advanced))
                    {
                        traveled += advanced;
                        continue;
                    }
                }
                else _local = AtDistance(location, distance + Math.Min(pending, available), length);
                if (pending <= available)
                {
                    remaining = 0f;
                    return;
                }

                traveled += available;
                // Identical exit and reservations imply the same lap. Keep an exact final lap for endpoint ownership.
                var key = new Exit(edge, location.IsForward, Wheel.ReservedRoutes.Count);
                visits ??= new();
                if (visits.TryGetValue(key, out var previous))
                {
                    var lap = traveled - previous;
                    if (lap <= 0d) break;
                    var tail = (requested % lap - traveled % lap) % lap;
                    if (tail < 0d) tail += lap;
                    if (tail == 0d) { remaining = 0f; return; }
                    requested = tail;
                    traveled = 0d;
                    visits.Clear();
                }
                else visits.Add(key, traveled);

                if (!Wheel.TryExit(edge, location.IsForward, _turnAngle, EnterRollingEdge)) break;
            }
            remaining = (float)(requested - traveled);
        }

        private bool EnterRollingEdge(IReadOnlySegment<TPosition, TRotation> edge, bool forward)
        {
            var location = new Location(0f, isForward: forward);
            if (edge is IClip || edge is IRoute)
            {
                var path = TerminalPath.For(edge);
                if (!TryLocate(path, location, out var segment, out var local, out var index)) return false;
                _rollingEdge = edge;
                _routeLocation = location;
                SetTerminal(path, segment, local, index);
            }
            else
            {
                LeaveRoute();
                _segment = edge;
                _local = location;
            }
            return true;
        }

        private double DirectedDistance(Location location, float length)
        {
            var distance = location.DistanceType == DistanceType.Rate
                ? (double)location.Distance * length : location.Distance;
            return Math.Clamp(distance, 0d, length);
        }

        private Location AtDistance(Location location, double distance, float length) => new(
            (float)(location.DistanceType == DistanceType.Rate ? distance / length : distance),
            location.DistanceType, location.IsForward);

        private Location Reverse(Location location, float length) => new(
            (location.DistanceType == DistanceType.Rate ? 1f : length) - location.Distance,
            location.DistanceType, !location.IsForward);
    }
}
