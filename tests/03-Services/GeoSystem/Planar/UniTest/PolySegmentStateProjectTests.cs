using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Planar;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem.Planar;
using BlackThunder.UniTest;
using NUnit.Framework;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("GeoSystemPlanar")]
    [Category("PolySegment")]
    public class PolySegmentStateProjectTests
    {
        private static readonly string[] _path =
        {
            "AddHandle", "QuadraticBezierShaper", "MoveHandle", "MoveFrom", "MoveTo", "Target",
            "AddHandle", "CubicBezierShaper",
            "ReplaceHandle", "DisconnectTo", "ReconnectTo", "DisposeHandle", "RemoveHandle", "QuadraticBezierShaper",
            "ClearHandles", "PolylineShaper", "ResetTarget", "Dispose", "Recreate", "Observe",
        };

        private class Scene : IDisposable
        {
            public Planar.Node From { get; } = new();
            public Planar.Node To { get; } = new(new Vector2(6f, 0f));
            public Planar.PolySegment Segment { get; }
            public SegmentPin Pin { get; }
            public Route Route { get; } = new();
            public List<Planar.ControlHandle> OwnedHandles { get; } = new();


            public Scene()
            {
                Segment = new Planar.PolySegment(From, To);
                Pin = new SegmentPin(Segment, new Location(0.5f, DistanceType.Rate));
                Route.Append(new SegmentView(Segment));
            }

            public Planar.ControlHandle CreateHandle(Vector2 position)
            {
                var handle = new Planar.ControlHandle(position);
                OwnedHandles.Add(handle);
                return handle;
            }

            public void Dispose()
            {
                Route.Dispose();
                Pin.Dispose();
                Segment.Dispose();
                foreach (var handle in OwnedHandles) handle.Dispose();
                From.Dispose();
                To.Dispose();
            }
        }

        private class PolyModel : Model
        {
            public Scene Scene => (Scene)Subject;
            public Vector2 From { get; set; }
            public Vector2 To { get; set; } = new(6f, 0f);
            public List<(Vector2 Position, bool Disposed)> Handles { get; } = new();
            public bool Connected { get; set; } = true;
            public bool Disposed { get; set; }
            public int Degree { get; set; }
            public float Target { get; set; } = 1f;
        }

        private class PolyProject : CompactProject<PolyModel>
        {
            private readonly bool _continuous;


            public PolyProject(bool continuous = false) : base(ExecutionMode.Inline) => _continuous = continuous;

            public override IEnumerable<CompactLab<PolyModel>> CreateCompactLabs(PolyModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return Checked("Create", state => state.Subject = new Scene());
                    yield break;
                }
                if (!_continuous && model.ExecutionCount > _path.Length) yield break;
                foreach (var lab in Operations(model))
                    if (_continuous || lab.Id == _path[model.ExecutionCount - 1]) yield return lab;
            }

            private IEnumerable<CompactLab<PolyModel>> Operations(PolyModel model)
            {
                yield return Checked("Observe", _ => { });
                yield return Checked("Recreate", state =>
                {
                    state.Scene.Dispose();
                    state.Subject = new Scene();
                    state.From = Vector2.Zero;
                    state.To = new Vector2(6f, 0f);
                    state.Handles.Clear();
                    state.Connected = true;
                    state.Disposed = false;
                    state.Degree = 0;
                    state.Target = 1f;
                });
                if (model.Disposed) yield break;

                yield return Checked("MoveFrom", state =>
                {
                    state.From = new Vector2(-1f - state.ExecutionCount % 3, 0f);
                    state.Scene.From.Position = state.From;
                });
                yield return Checked("MoveTo", state =>
                {
                    state.To = new Vector2(6f + state.ExecutionCount % 3, 1f);
                    state.Scene.To.Position = state.To;
                });
                if (model.Handles.Count < 3)
                {
                    yield return Checked("AddHandle", state =>
                    {
                        var point = new Vector2(2f + state.Handles.Count, 2f + state.ExecutionCount % 3);
                        state.Scene.Segment.Handles.Add(state.Scene.CreateHandle(point));
                        state.Handles.Add((point, false));
                    });
                }
                if (model.Handles.Count > 0)
                {
                    yield return Checked("RemoveHandle", state =>
                    {
                        state.Scene.Segment.Handles.RemoveAt(state.Handles.Count - 1);
                        state.Handles.RemoveAt(state.Handles.Count - 1);
                    });
                    yield return Checked("ReplaceHandle", state =>
                    {
                        var point = new Vector2(3f, 2f + state.ExecutionCount % 3);
                        state.Scene.Segment.Handles[0] = state.Scene.CreateHandle(point);
                        state.Handles[0] = (point, false);
                    });
                    if (!model.Handles[0].Disposed)
                    {
                        yield return Checked("MoveHandle", state =>
                        {
                            var point = new Vector2(2f, 1f + state.ExecutionCount % 4);
                            state.Scene.Segment.Handles[0].Position = point;
                            state.Handles[0] = (point, false);
                        });
                    }
                    if (!model.Handles.Last().Disposed)
                    {
                        yield return Checked("DisposeHandle", state =>
                        {
                            var index = state.Handles.Count - 1;
                            state.Scene.Segment.Handles[index].Dispose();
                            state.Handles[index] = (state.Handles[index].Position, true);
                        });
                    }
                }
                yield return Checked("ClearHandles", state =>
                {
                    state.Scene.Segment.Handles.Clear();
                    state.Handles.Clear();
                });
                yield return Checked("Target", state =>
                {
                    state.Target = 0.25f + 0.25f * (state.ExecutionCount % 4);
                    state.Scene.Segment.Values[ShaperValues.TargetSegmentLength] = state.Target;
                });
                yield return Checked("ResetTarget", state =>
                {
                    state.Scene.Segment.Values.Clear();
                    state.Target = 1f;
                });
                yield return Checked("PolylineShaper", state => SetShaper(state, new PolylineShaper(), 0));
                yield return Checked("QuadraticBezierShaper",
                    state => SetShaper(state, new QuadraticBezierShaper(), 2));
                yield return Checked("CubicBezierShaper", state => SetShaper(state, new CubicBezierShaper(), 3));
                yield return Checked("DisconnectTo", state =>
                {
                    state.Scene.Segment.DisconnectNode(ConnectionSide.To);
                    state.Connected = false;
                });
                yield return Checked("ReconnectTo", state =>
                {
                    state.Scene.Segment.ConnectNode(ConnectionSide.To, state.Scene.To);
                    state.Connected = true;
                });
                yield return Checked("Dispose", state =>
                {
                    state.Scene.Segment.Dispose();
                    state.Disposed = true;
                    state.Connected = false;
                });
            }

            private void SetShaper(PolyModel state, IShaper<Vector2> shaper, int degree)
            {
                state.Scene.Segment.Shaper = shaper;
                state.Degree = degree;
            }

            private CompactLab<PolyModel> Checked(string id, Action<PolyModel> actor) =>
                new(id, actor: actor, asserter: Check);

            private void Check(PolyModel state)
            {
                var scene = state.Scene;
                var segment = scene.Segment;
                Assert.That(segment.IsDisposed, Is.EqualTo(state.Disposed));
                Assert.That(segment.Handles.Select(handle => handle.Position),
                    Is.EqualTo(state.Handles.Select(handle => handle.Position)));
                var valid = !state.Disposed && state.Connected && state.Handles.All(handle => !handle.Disposed)
                    && (state.Degree == 0 || state.Handles.Count == state.Degree - 1);
                Assert.That(scene.Pin.IsDisposed && scene.Route.IsDisposed, Is.EqualTo(state.Disposed));
                if (!valid)
                {
                    Assert.That(segment.Hinges, Is.Empty);
                    Assert.That(segment.Length, Is.Zero);
                    if (!state.Disposed) Assert.That(segment.TryGetPosition(default, out _), Is.False);
                    return;
                }

                Assert.That(segment.Hinges.First().Position, Is.EqualTo(state.From));
                Assert.That(segment.Hinges.Last().Position, Is.EqualTo(state.To));
                Assert.That(segment.Hinges.First().Distance, Is.Zero);
                Assert.That(segment.Hinges.Last().Distance, Is.EqualTo(segment.Length));
                double expectedLength = 0;
                var previous = state.From;
                if (state.Degree == 0)
                {
                    foreach (var point in state.Handles.Select(handle => handle.Position).Append(state.To))
                    {
                        expectedLength += Vector2.Distance(previous, point);
                        previous = point;
                    }
                }
                else
                {
                    for (int i = 1; i <= 1000; i++)
                    {
                        var t = i / 1000f;
                        var u = 1f - t;
                        var point = state.Degree == 2
                            ? u * u * state.From + 2f * u * t * state.Handles[0].Position + t * t * state.To
                            : u * u * u * state.From + 3f * u * u * t * state.Handles[0].Position
                                + 3f * u * t * t * state.Handles[1].Position + t * t * t * state.To;
                        expectedLength += Vector2.Distance(previous, point);
                        previous = point;
                    }
                }
                Assert.That(segment.Length, Is.EqualTo(expectedLength).Within(state.Degree == 0 ? 0.0001d : 0.1d));
                Assert.That(scene.Route.Length, Is.EqualTo(segment.Length));
                Assert.That(scene.Pin.IsValid, Is.True);
                Assert.That(segment.TryGetPosition(new Location(0.5f, DistanceType.Rate), out var position), Is.True);
                Assert.That(scene.Pin.Position, Is.EqualTo(position));
                for (int i = 1; i < segment.Hinges.Count; i++)
                {
                    Assert.That(segment.Hinges[i].Distance, Is.GreaterThan(segment.Hinges[i - 1].Distance));
                    Assert.That(Vector2.Distance(segment.Hinges[i - 1].Position, segment.Hinges[i].Position),
                        Is.LessThanOrEqualTo(state.Target + 0.00001f));
                }
            }
        }


        [Test]
        public async Task FixedPolySegmentTransitions()
        {
            var result = await new PolyProject().Execute(_path.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_path.Length + 1));
        }

        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public Task RandomPolySegmentTransitions(int seed) =>
            RandomContinuousTest.RunAsync(new PolyProject(continuous: true), seed);
    }
}
