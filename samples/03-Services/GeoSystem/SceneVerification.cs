using System;
using System.Numerics;
using BlackThunder.GeoSystem.RouteSystem;

namespace BlackThunder.UniEngine.Samples.GeoSystem
{
    internal static class SceneVerification
    {
        public static void Run()
        {
            using var scene = new GeoDemoScene();
            Require(scene.Nodes.Count == 4 && scene.Segments.Count == 3, "graph construction");
            Require(scene.Clip.IsValid && scene.Clip.IsForward, "initial clip");
            Require(Math.Abs(scene.Clip.Length - scene.Segments[1].Length * 0.6f) < 0.0001f, "clip length");
            var length = scene.Route.Length;
            scene.MoveNode(1, scene.Nodes[1].Position + new Vector2(0f, 3f));
            Require(scene.Route.Length > length, "node movement propagates to route length");
            Require(scene.Events.Count > 1, "engine notifications are recorded");
            scene.SetClipRate(true, 0.9f);
            scene.SetClipRate(false, 0.1f);
            Require(scene.Clip.IsValid && !scene.Clip.IsForward, "clip reversal follows physical pin positions");
            scene.SetClipRate(false, 0.9f);
            Require(!scene.Clip.IsValid, "coincident pins invalidate the clip");
            scene.SetClipRate(false, 0.1f);
            Require(scene.Clip.IsValid, "clip recovers after a pin moves");
            scene.SetProgress(0f);
            Require(scene.Traveller.Position == scene.Nodes[0].Position, "forward origin");
            scene.SetForward(false);
            Require(scene.Traveller.Position == scene.Nodes[3].Position, "reverse origin");
            Require(scene.Hold.Position == scene.Traveller.Position && scene.Hold.IsValid, "hold follows its pin");
            scene.SetProgress(0.3f);
            scene.SetPlaying(true);
            scene.Advance(0.5f);
            var progress = scene.Progress;
            Require(progress > 0.3f, "playback advances by elapsed time");
            scene.SetPlaying(false);
            scene.Advance(1f);
            Require(scene.Progress == progress, "pause preserves progress");
            Require(scene.TryGetPose(out _), "pose is supplied by the route");
            var previousRoute = scene.Route;
            var previousNode = scene.Nodes[0];
            scene.Reset();
            Require(previousRoute.IsDisposed && previousNode.IsDisposed, "reset releases the previous graph");
            Require(!scene.IsPlaying && scene.IsForward && scene.Clip.IsValid, "reset restores the initial state");
            var route = scene.Route;
            var hold = scene.Hold;
            scene.Dispose();
            Require(route.IsDisposed && hold.IsDisposed, "scene disposal releases owned objects");
            Console.WriteLine("GeoSystem scene verification passed (17 checks).");
            VerifyTracers();
        }

        private static void VerifyTracers()
        {
            using var scene = new TracerDemoScene();
            Require(scene.Relative.State == TracerState.Tracking && scene.Absolute.State == TracerState.Tracking,
                "both tracers start tracking");
            Require(scene.Absolute.TryGetTransform(out var start, out _)
                && scene.Relative.TryGetTransform(out var same, out _) && Vector2.Distance(same, start) < 0.0001f,
                "the two markers start together");
            scene.Graph.MoveNode(0, scene.Graph.Nodes[0].Position - new Vector2(1f, 0f));
            Require(scene.Absolute.TryGetTransform(out var anchored, out _) && Vector2.Distance(start, anchored) < 0.0001f
                && scene.Relative.TryGetTransform(out var relative, out _) && Vector2.Distance(start, relative) > 0.1f,
                "editing the preceding edge separates the two position sources");
            scene.Place();
            scene.ReserveBranch();
            scene.Detach();
            Require(scene.Relative.State == TracerState.Derailed && scene.Absolute.State == TracerState.Rolling,
                "detaching separates Derailed and Rolling");
            scene.SetFailureOption(true);
            scene.Move(1f);
            Require(scene.AbsoluteMove == (false, 1f) && scene.Absolute.Wheel.ReservedRoutes.Count == 1,
                "TrackingFailure preserves the position, request distance and reservation");
            scene.SetFailureOption(false);
            scene.Move(1f);
            Require(scene.RelativeMove == (false, 1f) && scene.AbsoluteMove == (true, 0f)
                && scene.Absolute.Wheel.ReservedRoutes.Count == 1, "only the rolling tracer moves before the junction");
            scene.Move(2f);
            Require(ReferenceEquals(scene.Absolute.Segment, scene.Branch) && scene.Absolute.Wheel.ReservedRoutes.Count == 0,
                "crossing C consumes index one and enters the polyline");
            scene.Move(20f);
            Require(scene.AbsoluteMove.Value.Remaining > 0f && scene.Absolute.TryGetTransform(out var end, out _)
                && Vector2.Distance(end, scene.BranchEnd.Position) < 0.0001f, "the polyline ends at E with excess distance");
            scene.Place();
            scene.Detach();
            scene.Move(3f);
            Require(ReferenceEquals(scene.Absolute.Segment, scene.Graph.Segments[2]),
                "without a reservation the smallest turn leads to D");
            Console.WriteLine("Tracer and Wheel comparison passed (9 checks).");
        }

        private static void Require(bool condition, string contract)
        {
            if (!condition) throw new InvalidOperationException("Scenario verification failed: " + contract);
        }
    }
}
