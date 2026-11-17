using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem.Planar;
using BlackThunder.UniEngine.Core;
using BlackThunder.UniTest;
using NUnit.Framework;
using Planar = BlackThunder.GeoSystem.Geometry.Planar;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("RouteEditing")]
    public class RouteStateProjectTests
    {
        private static readonly string[] _path =
        {
            "From0", "To2", "Block", "From1", "DisconnectTo", "To1", "Unblock", "To2",
            "From2", "DisconnectFrom", "From0", "Resize", "DisconnectNode0", "Clear",
            "Append", "Remove", "Dispose", "Recreate", "Observe",
        };

        private class Scene : IDisposable
        {
            public Planar.Node[] Nodes { get; } = Enumerable.Range(0, 3)
                .Select(index => new Planar.Node(new Vector2(index, 0f))).ToArray();
            public Planar.LinearSegment[] Edges { get; }
            public Route Route { get; } = new();


            public Scene()
            {
                Edges = new[]
                {
                    new Planar.LinearSegment(Nodes[0], Nodes[1]),
                    new Planar.LinearSegment(Nodes[1], Nodes[2]),
                };
                Route.Property.Add("marker", 7);
            }

            public void Dispose()
            {
                Route.Dispose();
                foreach (var edge in Edges) edge.Dispose();
                foreach (var node in Nodes) node.Dispose();
            }
        }

        private class RouteModel : Model
        {
            public Scene Scene => (Scene)Subject;
            public int From { get; set; } = -1;
            public int To { get; set; } = -1;
            public List<int> Views { get; } = new();
            public bool Blocked { get; set; }
            public bool Disposed { get; set; }
            public int Scale { get; set; } = 1;
        }

        private class RouteProject : CompactProject<RouteModel>
        {
            private readonly bool _continuous;


            public RouteProject(bool continuous = false) : base(ExecutionMode.Inline) => _continuous = continuous;

            public override IEnumerable<CompactLab<RouteModel>> CreateCompactLabs(RouteModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return Checked("Create", state => state.Subject = new Scene());
                    yield break;
                }
                if (!_continuous && model.ExecutionCount > _path.Length) yield break;
                foreach (var operation in Operations(model))
                    if (_continuous || operation.Id == _path[model.ExecutionCount - 1]) yield return operation;
            }

            private IEnumerable<CompactLab<RouteModel>> Operations(RouteModel model)
            {
                yield return Checked("Observe", _ => { });
                yield return Checked("Recreate", state =>
                {
                    state.Scene.Dispose();
                    state.Subject = new Scene();
                    state.From = state.To = -1;
                    state.Views.Clear();
                    state.Blocked = state.Disposed = false;
                    state.Scale = 1;
                });
                if (model.Disposed) yield break;

                for (int node = 0; node < 3; node++)
                {
                    var index = node;
                    yield return Checked($"From{index}", state => Connect(state, ConnectionSide.From, index));
                    yield return Checked($"To{index}", state => Connect(state, ConnectionSide.To, index));
                }
                yield return Checked("DisconnectFrom", state =>
                {
                    state.Scene.Route.DisconnectNode(ConnectionSide.From);
                    if (state.From < 0) return;
                    state.From = -1;
                    state.Views.Clear();
                });
                yield return Checked("DisconnectTo", state =>
                {
                    state.Scene.Route.DisconnectNode(ConnectionSide.To);
                    if (state.To < 0) return;
                    state.To = -1;
                    state.Views.Clear();
                });
                yield return Checked("DisconnectNode0", state =>
                {
                    state.Scene.Route.DisconnectNode(state.Scene.Nodes[0]);
                    if (state.From != 0 && state.To != 0) return;
                    if (state.From == 0) state.From = -1;
                    if (state.To == 0) state.To = -1;
                    state.Views.Clear();
                });
                yield return Checked("Clear", state =>
                {
                    state.Scene.Route.Clear();
                    state.From = state.To = -1;
                    state.Views.Clear();
                });
                yield return Checked("Block", state =>
                {
                    state.Scene.Edges[1].TraversalOptions = TraversalOptions.None;
                    state.Blocked = true;
                });
                yield return Checked("Unblock", state =>
                {
                    state.Scene.Edges[1].TraversalOptions = TraversalOptions.Both;
                    state.Blocked = false;
                });
                yield return Checked("Resize", state =>
                {
                    state.Scale = 1 + state.ExecutionCount % 4;
                    for (int i = 0; i < 3; i++) state.Scene.Nodes[i].Position = new Vector2(i * state.Scale, 0f);
                });
                yield return Checked("Append", state =>
                {
                    state.Scene.Route.Append(new SegmentView(state.Scene.Edges[0]));
                    state.Views.Add(1);
                    DeriveEndpoints(state);
                });
                yield return Checked("Remove", state =>
                {
                    state.Scene.Route.Remove(state.Scene.Edges[0]);
                    if (state.Views.Count == 0) return;
                    if (Math.Abs(state.Views[0]) == 1) state.Views.RemoveAt(0);
                    else if (Math.Abs(state.Views.Last()) == 1) state.Views.RemoveAt(state.Views.Count - 1);
                    DeriveEndpoints(state);
                });
                yield return Checked("Dispose", state =>
                {
                    state.Scene.Route.Dispose();
                    state.Disposed = true;
                    state.From = state.To = -1;
                    state.Views.Clear();
                });
            }

            private void Connect(RouteModel state, ConnectionSide side, int node)
            {
                var current = side == ConnectionSide.From ? state.From : state.To;
                var from = side == ConnectionSide.From ? node : state.From;
                var to = side == ConnectionSide.To ? node : state.To;
                var blocked = from >= 0 && to >= 0 && from != to && state.Blocked && (from == 2 || to == 2);
                var expected = current == node || !blocked;
                Assert.That(state.Scene.Route.TryConnectNode(side, state.Scene.Nodes[node]), Is.EqualTo(expected));
                if (!expected || current == node) return;
                state.From = from;
                state.To = to;
                state.Views.Clear();
                if (from < 0 || to < 0) return;
                for (int i = from; i < to; i++) state.Views.Add(i + 1);
                for (int i = from; i > to; i--) state.Views.Add(-i);
            }

            private void DeriveEndpoints(RouteModel state)
            {
                state.From = state.Views.Count == 0 ? -1 : Start(state.Views[0]);
                state.To = state.Views.Count == 0 ? -1 : End(state.Views.Last());
            }

            private int Start(int view) => view > 0 ? view - 1 : -view;
            private int End(int view) => view > 0 ? view : -view - 1;
            private CompactLab<RouteModel> Checked(string id, Action<RouteModel> actor) =>
                new(id, actor: actor, asserter: Check);

            private void Check(RouteModel state)
            {
                var scene = state.Scene;
                var route = scene.Route;
                Assert.That(route.From, Is.SameAs(state.From < 0 ? null : scene.Nodes[state.From]));
                Assert.That(route.To, Is.SameAs(state.To < 0 ? null : scene.Nodes[state.To]));
                Assert.That(route.IsDisposed, Is.EqualTo(state.Disposed));
                Assert.That(route.Views.Count, Is.EqualTo(state.Views.Count));
                Assert.That(route.IsIsolated, Is.EqualTo(state.From < 0 && state.To < 0 && state.Views.Count == 0));
                Assert.That(route.Length, Is.EqualTo(state.Views.Count * state.Scale));
                Assert.That(route.Property.Get<int>("marker"), Is.EqualTo(7));
                Assert.That(route.TraversalOptions, Is.EqualTo(TraversalOptions.Both));
                var nodes = new HashSet<int>();
                if (state.From >= 0) nodes.Add(state.From);
                if (state.To >= 0) nodes.Add(state.To);
                for (int i = 0; i < state.Views.Count; i++)
                {
                    var view = state.Views[i];
                    Assert.That(route.Views[i].Source, Is.SameAs(scene.Edges[Math.Abs(view) - 1]));
                    Assert.That(route.Views[i].IsForward, Is.EqualTo(view > 0));
                    nodes.Add(Start(view));
                    nodes.Add(End(view));
                }
                Assert.That(route.Nodes, Is.EquivalentTo(nodes.Select(index => scene.Nodes[index])));
                for (int i = 0; i < 2; i++)
                {
                    var used = state.Views.Any(view => Math.Abs(view) == i + 1);
                    Assert.That(((EventHub<int>)scene.Edges[i].EventHub).ConnectionCount, Is.EqualTo(used ? 1 : 0));
                }
                for (int i = 0; i < 3; i++)
                {
                    var degree = i == 1 ? 2 : 1;
                    var anchored = state.Views.Count == 0 && (state.From == i || state.To == i);
                    Assert.That(scene.Nodes[i].Segments.Count(), Is.EqualTo(degree));
                    Assert.That(((EventHub<int>)scene.Nodes[i].EventHub).ConnectionCount,
                        Is.EqualTo(degree + (anchored ? 1 : 0)));
                }
            }
        }


        [Test]
        public async Task FixedRouteEditingTransitions()
        {
            var result = await new RouteProject().Execute(_path.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_path.Length + 1));
        }

        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public Task RandomRouteEditingTransitions(int seed) =>
            RandomContinuousTest.RunAsync(new RouteProject(continuous: true), seed);
    }
}
