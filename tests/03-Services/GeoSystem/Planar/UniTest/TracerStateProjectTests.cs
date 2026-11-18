using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.RouteSystem;
using BlackThunder.GeoSystem.RouteSystem.Planar;
using BlackThunder.UniTest;
using NUnit.Framework;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;
using Route = BlackThunder.GeoSystem.RouteSystem.Planar.Route;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("Tracer")]
    public class TracerStateProjectTests
    {
        private class Scene
        {
            public Planar.LinearSegment Edge { get; } = new(
                new Planar.Node(Vector2.Zero), new Planar.Node(new Vector2(8, 0)));
            public Route Route { get; } = new();
            public Tracer<Vector2, float> Tracer { get; } = new();
        }

        private class TracerModel : Model
        {
            public Scene Scene => (Scene)Subject;
            public TracerState Expected { get; set; }
            public bool Absolute { get; set; }
            public bool Forward { get; set; } = true;
            public bool Strict { get; set; }
            public float Position { get; set; }
        }

        private class TracerProject : CompactProject<TracerModel>
        {
            public TracerProject() : base(ExecutionMode.Inline) { }

            public override IEnumerable<CompactLab<TracerModel>> CreateCompactLabs(TracerModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return Operation("Create", m => m.Subject = new Scene());
                    yield break;
                }

                yield return Operation("PlaceAbsolute", m => Place(m, true));
                yield return Operation("PlaceRouteBased", m => Place(m, false));
                yield return Operation("ClearRoute", m =>
                {
                    m.Scene.Route.Clear();
                    if (m.Expected == TracerState.Tracking)
                        m.Expected = m.Absolute ? TracerState.Rolling : TracerState.Derailed;
                });
                yield return Operation("FailureOption", m =>
                {
                    m.Strict = !m.Strict;
                    m.Scene.Tracer.Wheel.MoveFailureOption = m.Strict
                        ? MoveFailureOption.TrackingFailure : MoveFailureOption.Derailed;
                });
                yield return Operation("MoveThree", m =>
                {
                    var allowed = m.Expected == TracerState.Tracking
                        || (m.Expected == TracerState.Rolling && !m.Strict);
                    var distance = allowed ? Math.Min(3f, m.Forward ? 8f - m.Position : m.Position) : 0f;
                    Assert.That(m.Scene.Tracer.Move(3f, out var state, out var remaining), Is.EqualTo(allowed));
                    Assert.That(state, Is.EqualTo(m.Expected));
                    Assert.That(remaining, Is.EqualTo(3f - distance));
                    m.Position += m.Forward ? distance : -distance;
                });
                if (model.Expected != TracerState.Derailed) yield return Operation("Reverse", m =>
                {
                    m.Forward = !m.Forward;
                    m.Scene.Tracer.IsForward = m.Forward;
                });
            }

            private void Place(TracerModel model, bool absolute)
            {
                var scene = model.Scene;
                if (scene.Route.Views.Count == 0) scene.Route.Append(new SegmentView(scene.Edge));
                var type = absolute ? LocationType.Absolute : LocationType.RouteBased;
                Assert.That(scene.Tracer.PlaceOnRoute(scene.Route, new Location(2f), type), Is.True);
                model.Expected = TracerState.Tracking;
                model.Absolute = absolute;
                model.Forward = true;
                model.Position = 2f;
            }

            private CompactLab<TracerModel> Operation(string id, Action<TracerModel> action) =>
                new(id, actor: action, asserter: m =>
                {
                    var tracer = m.Scene.Tracer;
                    Assert.That(tracer.State, Is.EqualTo(m.Expected));
                    bool valid = m.Expected != TracerState.Derailed;
                    Assert.That(tracer.TryGetTransform(out var position, out _), Is.EqualTo(valid));
                    Assert.That(tracer.Segment, Is.SameAs(valid ? m.Scene.Edge : null));
                    if (valid) Assert.That(position, Is.EqualTo(new Vector2(m.Position, 0f)));
                });
        }


        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public Task RandomTracerTransitions(int seed) => RandomContinuousTest.RunAsync(new TracerProject(), seed);
    }
}
