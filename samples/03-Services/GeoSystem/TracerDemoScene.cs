using System;
using System.Numerics;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.RouteSystem;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;

namespace BlackThunder.UniEngine.Samples.GeoSystem
{
    internal sealed class TracerDemoScene : IDisposable
    {
        public GeoDemoScene Graph { get; } = new();
        public Planar.Node BranchEnd { get; } = new(new Vector2(6f, 3.5f));
        public Planar.ControlHandle Bend { get; } = new(new Vector2(3.5f, 3.5f));
        public Planar.PolySegment Branch { get; }
        public Tracer<Vector2, float> Relative { get; private set; }
        public PolyTracer<Vector2, float> Absolute { get; private set; }
        public (bool Success, float Remaining)? RelativeMove { get; private set; }
        public (bool Success, float Remaining)? AbsoluteMove { get; private set; }
        public event Action Changed;


        public TracerDemoScene()
        {
            Branch = new Planar.PolySegment(Graph.Nodes[2], BranchEnd);
            Branch.Handles.Add(Bend);
            Graph.Changed += Notify;
            Place();
        }

        public void Place()
        {
            var policy = Absolute?.Wheel.MoveFailureOption ?? MoveFailureOption.Derailed;
            Relative = new Tracer<Vector2, float>();
            Absolute = new PolyTracer<Vector2, float>();
            Absolute.Wheel.MoveFailureOption = policy;
            var middle = new Location(Graph.Segments[0].Length + Graph.Segments[1].Length / 2f);
            Relative.PlaceOnRoute(Graph.Route, middle, LocationType.RouteBased);
            Absolute.PlaceOnRoute(Graph.Route, middle, LocationType.Absolute);
            RelativeMove = AbsoluteMove = null;
            Notify();
        }

        public void Detach()
        {
            Relative.DetachRoute();
            Absolute.DetachRoute();
            RelativeMove = AbsoluteMove = null;
            Notify();
        }

        public void ReserveBranch()
        {
            // C-D is candidate 0 and C-E is candidate 1 when arriving from B.
            Absolute.Wheel.ReservedRoutes.Enqueue(1);
            Notify();
        }

        public void SetFailureOption(bool requireTracking)
        {
            Absolute.Wheel.MoveFailureOption = requireTracking ? MoveFailureOption.TrackingFailure : MoveFailureOption.Derailed;
            Notify();
        }

        public void Move(float distance)
        {
            var relativeMoved = Relative.Move(distance, out _, out var relativeRemaining);
            var absoluteMoved = Absolute.Move(distance, out _, out var absoluteRemaining);
            RelativeMove = (relativeMoved, relativeRemaining);
            AbsoluteMove = (absoluteMoved, absoluteRemaining);
            Notify();
        }

        public string SegmentName(IReadOnlySegment segment)
        {
            if (ReferenceEquals(segment, Branch)) return "C–E";
            for (int i = 0; i < Graph.Segments.Count; i++)
                if (ReferenceEquals(segment, Graph.Segments[i])) return $"{(char)('A' + i)}–{(char)('B' + i)}";
            return "—";
        }

        private void Notify() => Changed?.Invoke();

        public void Dispose()
        {
            Graph.Changed -= Notify;
            Branch.Dispose();
            Bend.Dispose();
            BranchEnd.Dispose();
            Graph.Dispose();
        }
    }
}
