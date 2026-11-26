using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.RouteSystem;
using BlackThunder.UniTest;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("GeoGroups")]
    public class GroupStateProjectTests
    {
        private static readonly string[] _path =
        {
            "AddHold0", "AddHold1", "AddHold0", "AddRoute0", "AddChild", "Observe",
            "RemoveHold0", "AddHold0", "DisposeHold1", "AddHold1", "DisposeRoute0", "DisposeChild",
            "Clear", "AddHold0", "DisposeGroups", "AddHold0", "Recreate", "AddChild", "Clear",
        };

        private class Scene : IDisposable
        {
            public NavGroup Nav { get; } = new();
            public GeoGroup<float, bool> Geo { get; } = new();
            public RouteGroup<float, bool> Route { get; } = new();
            public NavGroup NavChild { get; } = new();
            public GeoGroup<float, bool> GeoChild { get; } = new();
            public RouteGroup<float, bool> RouteChild { get; } = new();
            public Hold<float, bool>[] Holds { get; } = { new(), new() };
            public Route<float, bool>[] Routes { get; } = { new(), new() };

            public void DisposeGroups()
            {
                Nav.Dispose();
                Geo.Dispose();
                Route.Dispose();
            }

            public void DisposeChild()
            {
                NavChild.Dispose();
                GeoChild.Dispose();
                RouteChild.Dispose();
            }

            public void Dispose()
            {
                DisposeGroups();
                DisposeChild();
                foreach (var hold in Holds) hold.Dispose();
                foreach (var route in Routes) route.Dispose();
            }
        }

        private class GroupModel : Model
        {
            public Scene Scene => (Scene)Subject;
            public List<int> Holds { get; } = new();
            public List<int> Routes { get; } = new();
            public bool[] DeadHolds { get; } = new bool[2];
            public bool[] DeadRoutes { get; } = new bool[2];
            public bool Child { get; set; }
            public bool DeadChild { get; set; }
            public bool Disposed { get; set; }
        }

        private class GroupProject : CompactProject<GroupModel>
        {
            private readonly bool _continuous;
            public GroupProject(bool continuous = false) : base(ExecutionMode.Inline) => _continuous = continuous;

            public override IEnumerable<CompactLab<GroupModel>> CreateCompactLabs(GroupModel model)
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

            private IEnumerable<CompactLab<GroupModel>> Operations()
            {
                yield return Checked("Observe", _ => { });
                yield return Checked("Recreate", state =>
                {
                    state.Scene.Dispose();
                    state.Subject = new Scene();
                    state.Holds.Clear();
                    state.Routes.Clear();
                    Array.Clear(state.DeadHolds, 0, 2);
                    Array.Clear(state.DeadRoutes, 0, 2);
                    state.Child = state.DeadChild = state.Disposed = false;
                });
                yield return Checked("Clear", state =>
                {
                    if (state.Disposed)
                    {
                        Assert.Throws<ObjectDisposedException>(state.Scene.Nav.Clear);
                        Assert.Throws<ObjectDisposedException>(state.Scene.Geo.Clear);
                        Assert.Throws<ObjectDisposedException>(state.Scene.Route.Clear);
                        return;
                    }
                    state.Scene.Nav.Clear();
                    state.Scene.Geo.Clear();
                    state.Scene.Route.Clear();
                    state.Holds.Clear();
                    state.Routes.Clear();
                    state.Child = false;
                });
                yield return Checked("DisposeGroups", state =>
                {
                    state.Scene.DisposeGroups();
                    state.Disposed = true;
                    state.Holds.Clear();
                    state.Routes.Clear();
                    state.Child = false;
                });
                yield return Checked("AddChild", state =>
                {
                    CheckAdd(state.Disposed, state.DeadChild, !state.Child, () =>
                        state.Scene.Nav.AddGroup(state.Scene.NavChild));
                    CheckAdd(state.Disposed, state.DeadChild, !state.Child, () =>
                        state.Scene.Geo.AddGroup(state.Scene.GeoChild));
                    CheckAdd(state.Disposed, state.DeadChild, !state.Child, () =>
                        state.Scene.Route.AddGroup(state.Scene.RouteChild));
                    if (!state.Disposed && !state.DeadChild) state.Child = true;
                });
                yield return Checked("DisposeChild", state =>
                {
                    state.Scene.DisposeChild();
                    state.DeadChild = true;
                    state.Child = false;
                });
                for (int index = 0; index < 2; index++)
                {
                    var i = index;
                    yield return Checked($"AddHold{i}", state =>
                    {
                        var member = state.Scene.Holds[i];
                        var added = !state.Holds.Contains(i);
                        CheckAdd(state.Disposed, state.DeadHolds[i], added, () => state.Scene.Nav.AddNode(member));
                        CheckAdd(state.Disposed, state.DeadHolds[i], added, () => state.Scene.Geo.AddNode(member));
                        CheckAdd(state.Disposed, state.DeadHolds[i], added, () => state.Scene.Route.AddHold(member));
                        if (!state.Disposed && !state.DeadHolds[i] && added) state.Holds.Add(i);
                    });
                    yield return Checked($"AddRoute{i}", state =>
                    {
                        var member = state.Scene.Routes[i];
                        var added = !state.Routes.Contains(i);
                        CheckAdd(state.Disposed, state.DeadRoutes[i], added, () => state.Scene.Nav.AddSegment(member));
                        CheckAdd(state.Disposed, state.DeadRoutes[i], added, () => state.Scene.Geo.AddSegment(member));
                        CheckAdd(state.Disposed, state.DeadRoutes[i], added, () => state.Scene.Route.AddRoute(member));
                        if (!state.Disposed && !state.DeadRoutes[i] && added) state.Routes.Add(i);
                    });
                    yield return Checked($"RemoveHold{i}", state =>
                    {
                        var member = state.Scene.Holds[i];
                        var removed = state.Holds.Contains(i);
                        CheckAdd(state.Disposed, false, removed, () => state.Scene.Nav.RemoveNode(member));
                        CheckAdd(state.Disposed, false, removed, () => state.Scene.Geo.RemoveNode(member));
                        CheckAdd(state.Disposed, false, removed, () => state.Scene.Route.RemoveHold(member));
                        state.Holds.Remove(i);
                    });
                    yield return Checked($"DisposeHold{i}", state =>
                    {
                        state.Scene.Holds[i].Dispose();
                        state.DeadHolds[i] = true;
                        state.Holds.Remove(i);
                    });
                    yield return Checked($"DisposeRoute{i}", state =>
                    {
                        state.Scene.Routes[i].Dispose();
                        state.DeadRoutes[i] = true;
                        state.Routes.Remove(i);
                    });
                }
            }

            private void CheckAdd(bool disposed, bool dead, bool expected, Func<bool> operation)
            {
                if (disposed) Assert.Throws<ObjectDisposedException>(() => operation());
                else if (dead) Assert.Throws<ArgumentException>(() => operation());
                else Assert.That(operation(), Is.EqualTo(expected));
            }

            private CompactLab<GroupModel> Checked(string id, Action<GroupModel> actor) =>
                new(id, actor: actor, asserter: state =>
                {
                    foreach (var group in new IReadOnlyNavGroup[]
                        { state.Scene.Nav, state.Scene.Geo, state.Scene.Route })
                    {
                        Assert.That(group.Nodes, Is.EqualTo(state.Holds.Select(i => state.Scene.Holds[i])));
                        Assert.That(group.Segments, Is.EqualTo(state.Routes.Select(i => state.Scene.Routes[i])));
                        Assert.That(group.NestedGroups.Count, Is.EqualTo(state.Child ? 1 : 0));
                        Assert.That(group.IsDisposed, Is.EqualTo(state.Disposed));
                    }
                    Assert.That(state.Scene.Holds.Select(item => item.IsDisposed), Is.EqualTo(state.DeadHolds));
                    Assert.That(state.Scene.Routes.Select(item => item.IsDisposed), Is.EqualTo(state.DeadRoutes));
                    Assert.That(state.Scene.NavChild.IsDisposed, Is.EqualTo(state.DeadChild));
                    Assert.That(state.Scene.GeoChild.IsDisposed, Is.EqualTo(state.DeadChild));
                    Assert.That(state.Scene.RouteChild.IsDisposed, Is.EqualTo(state.DeadChild));
                });
        }

        [Test]
        public async Task FixedGroupTransitions()
        {
            var result = await new GroupProject().Execute(_path.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_path.Length + 1));
        }

        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public Task RandomGroupTransitions(int seed) =>
            RandomContinuousTest.RunAsync(new GroupProject(continuous: true), seed);
    }
}
