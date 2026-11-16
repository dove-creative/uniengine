using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.GeoSystem.RouteSystem.Planar;
using BlackThunder.UniTest;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("GeoSystemPlanar")]
    public class PlanarStateProjectTests
    {
        private static readonly string[] _path =
        {
            "AppendForward", "AppendReverse", "Resize", "Translate", "MovePin", "Remove", "Clear",
            "AppendReverse", "Rebind", "Resize", "Translate", "Recreate", "AppendForward", "Observe"
        };

        private sealed class Scene : IDisposable
        {
            public Node From { get; } = new(Vector2.Zero);
            public Node To { get; } = new(new Vector2(10f, 0f));
            public LinearSegment Source { get; }
            public SegmentPin Pin { get; }
            public SegmentPin Start { get; }
            public SegmentPin End { get; }
            public Clip Clip { get; }
            public Hold Hold { get; } = new();
            public Route Route { get; } = new();

            public Scene()
            {
                Source = new LinearSegment(From, To);
                Pin = new SegmentPin(Source, new Location(0.25f, DistanceType.Rate));
                Start = new SegmentPin(Source, new Location(0.2f, DistanceType.Rate));
                End = new SegmentPin(Source, new Location(0.8f, DistanceType.Rate));
                Clip = new Clip(Start, End);
                Hold.MoveTo(Pin);
            }

            public void Dispose()
            {
                Route.Dispose();
                Hold.Dispose();
                Clip.Dispose();
                Pin.Dispose();
                Start.Dispose();
                End.Dispose();
                Source.Dispose();
                From.Dispose();
                To.Dispose();
            }
        }

        private class SceneModel : Model
        {
            public Scene Scene => (Scene)Subject;
            public float Start { get; set; }
            public float Length { get; set; } = 10f;
            public float Rate { get; set; } = 0.25f;
            public List<bool> Views { get; } = new();
        }

        private class SceneProject : CompactProject<SceneModel>
        {
            private readonly bool _continuous;
            public SceneProject(bool continuous = false) : base(ExecutionMode.Inline) => _continuous = continuous;

            public override IEnumerable<CompactLab<SceneModel>> CreateCompactLabs(SceneModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return Checked("Create", state => state.Subject = new Scene());
                    yield break;
                }
                if (!_continuous && model.ExecutionCount > _path.Length) yield break;
                foreach (var lab in Operations())
                    if (_continuous || lab.Id == _path[model.ExecutionCount - 1]) yield return lab;
            }

            private IEnumerable<CompactLab<SceneModel>> Operations()
            {
                yield return Checked("Observe", _ => { });
                yield return Checked("Resize", state =>
                {
                    state.Length = 2f + state.ExecutionCount % 31;
                    state.Scene.To.Position = new Vector2(state.Start + state.Length, 0f);
                });
                yield return Checked("Translate", state =>
                {
                    state.Start += 1f;
                    state.Scene.From.Position = new Vector2(state.Start, 0f);
                    state.Scene.To.Position = new Vector2(state.Start + state.Length, 0f);
                });
                yield return Checked("MovePin", state =>
                {
                    state.Rate = (state.ExecutionCount % 11) / 10f;
                    state.Scene.Pin.Location = new Location(state.Rate, DistanceType.Rate);
                });
                yield return Checked("AppendForward", state => Append(state, true));
                yield return Checked("AppendReverse", state => Append(state, false));
                yield return Checked("Remove", state =>
                {
                    state.Scene.Route.Remove(state.Scene.Clip);
                    if (state.Views.Count > 0) state.Views.RemoveAt(0);
                });
                yield return Checked("Clear", state =>
                {
                    state.Scene.Route.ClearNodes();
                    state.Views.Clear();
                });
                yield return Checked("Rebind", state =>
                {
                    state.Scene.Hold.MoveTo(state.Scene.Source, new Location(state.Rate, DistanceType.Rate));
                    state.Scene.Hold.MoveTo(state.Scene.Pin);
                });
                yield return Checked("Recreate", state =>
                {
                    state.Scene.Dispose();
                    state.Subject = new Scene();
                    state.Start = 0f;
                    state.Length = 10f;
                    state.Rate = 0.25f;
                    state.Views.Clear();
                });
            }

            private static void Append(SceneModel state, bool forward)
            {
                state.Scene.Route.Append(new SegmentView(state.Scene.Clip, forward));
                state.Views.Add(forward);
            }

            private static CompactLab<SceneModel> Checked(string id, Action<SceneModel> actor) =>
                new(id, actor: actor, asserter: CheckScene);

            private static void CheckScene(SceneModel state)
            {
                var scene = state.Scene;
                Assert.That(scene.Source.Length, Is.EqualTo(state.Length).Within(0.0001f));
                var expected = new Vector2(state.Start + state.Length * state.Rate, 0f);
                Assert.That(Vector2.Distance(scene.Pin.Position, expected), Is.LessThan(0.0001f));
                Assert.That(scene.Hold.Position, Is.EqualTo(scene.Pin.Position));
                Assert.That(scene.Pin.IsValid && scene.Hold.IsValid && scene.Clip.IsValid, Is.True);
                Assert.That(scene.Clip.Length, Is.EqualTo(state.Length * 0.6f).Within(0.0001f));
                Assert.That(scene.Route.Views.Count, Is.EqualTo(state.Views.Count));
                Assert.That(scene.Route.Length, Is.EqualTo(state.Length * 0.6f * state.Views.Count).Within(0.001f));
                if (state.Views.Count == 0)
                {
                    Assert.That(scene.Route.TryGetPosition(default, out _), Is.False);
                    return;
                }
                var forward = state.Views[0];
                PlanarGeoSystemTests.AssertPose(scene.Route, default,
                    new Vector2(state.Start + state.Length * (forward ? 0.2f : 0.8f), 0f), forward ? 0f : 180f);
            }
        }


        [Test]
        public async Task FixedPlanarTransitions()
        {
            var result = await new SceneProject().Execute(_path.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_path.Length + 1));
        }

        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public Task RandomPlanarTransitions(int seed) =>
            RandomContinuousTest.RunAsync(new SceneProject(continuous: true), seed);
    }
}
