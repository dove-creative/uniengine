using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniTest;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    public class NavigationStateProjectTests
    {
        private static readonly string[] _path =
        {
            "Connect0From0", "Connect0To1", "Connect1From1", "Connect1To2", "Connect0From2",
            "Connect0To0", "ClearNode1", "Connect0To0", "Disconnect0From", "Connect0To0",
            "ClearSegment0", "ClearNode2", "Recreate", "Connect1To2", "Observe"
        };

        private sealed class Graph : IDisposable
        {
            public NavNode[] Nodes { get; } = { new(), new(), new() };
            public NavSegment[] Segments { get; } = { new(), new() };
            public void Dispose()
            {
                foreach (var node in Nodes) node.Dispose();
                foreach (var segment in Segments) segment.Dispose();
            }
        }

        private class GraphModel : Model
        {
            public Graph Graph => (Graph)Subject;
            public int[,] Ends { get; } = { { -1, -1 }, { -1, -1 } };
        }

        private class GraphProject : CompactProject<GraphModel>
        {
            private readonly bool _continuous;
            public GraphProject(bool continuous = false) : base(ExecutionMode.Inline) => _continuous = continuous;

            public override IEnumerable<CompactLab<GraphModel>> CreateCompactLabs(GraphModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return Checked("Create", state => state.Subject = new Graph());
                    yield break;
                }
                if (!_continuous && model.ExecutionCount > _path.Length) yield break;
                foreach (var lab in Operations())
                    if (_continuous || lab.Id == _path[model.ExecutionCount - 1]) yield return lab;
            }

            private IEnumerable<CompactLab<GraphModel>> Operations()
            {
                yield return Checked("Observe", _ => { });
                yield return Checked("Recreate", state =>
                {
                    state.Graph.Dispose();
                    Assert.That(state.Graph.Segments.All(segment => segment.IsIsolated && segment.IsDisposed), Is.True);
                    state.Subject = new Graph();
                    for (int i = 0; i < 2; i++)
                        for (int j = 0; j < 2; j++) state.Ends[i, j] = -1;
                });
                for (int segmentIndex = 0; segmentIndex < 2; segmentIndex++)
                {
                    var s = segmentIndex;
                    yield return Checked($"ClearSegment{s}", state =>
                    {
                        state.Graph.Segments[s].ClearNodes();
                        state.Ends[s, 0] = state.Ends[s, 1] = -1;
                    });
                    for (int sideIndex = 0; sideIndex < 2; sideIndex++)
                    {
                        var side = sideIndex;
                        yield return Checked($"Disconnect{s}{(ConnectionSide)side}", state =>
                        {
                            state.Graph.Segments[s].DisconnectNode((ConnectionSide)side);
                            state.Ends[s, side] = -1;
                        });
                        for (int nodeIndex = 0; nodeIndex < 3; nodeIndex++)
                        {
                            var n = nodeIndex;
                            yield return Checked($"Connect{s}{(ConnectionSide)side}{n}", state =>
                            {
                                var segment = state.Graph.Segments[s];
                                var node = state.Graph.Nodes[n];
                                if (state.Ends[s, side] >= 0 && state.Ends[s, side] != n)
                                {
                                    Assert.Throws<InvalidOperationException>(() =>
                                        segment.ConnectNode((ConnectionSide)side, node));
                                }
                                else if (state.Ends[s, 1 - side] == n)
                                {
                                    Assert.Throws<ArgumentException>(() =>
                                        segment.ConnectNode((ConnectionSide)side, node));
                                }
                                else
                                {
                                    segment.ConnectNode((ConnectionSide)side, node);
                                    state.Ends[s, side] = n;
                                }
                            });
                        }
                    }
                }
                for (int index = 0; index < 3; index++)
                {
                    var n = index;
                    yield return Checked($"ClearNode{n}", state =>
                    {
                        state.Graph.Nodes[n].ClearSegments();
                        for (int i = 0; i < 2; i++)
                            for (int j = 0; j < 2; j++)
                                if (state.Ends[i, j] == n) state.Ends[i, j] = -1;
                    });
                }
            }

            private static CompactLab<GraphModel> Checked(string id, Action<GraphModel> actor) =>
                new(id, actor: actor, asserter: CheckGraph);

            private static void CheckGraph(GraphModel state)
            {
                for (int s = 0; s < 2; s++)
                {
                    var segment = state.Graph.Segments[s];
                    var from = state.Ends[s, 0] < 0 ? null : state.Graph.Nodes[state.Ends[s, 0]];
                    var to = state.Ends[s, 1] < 0 ? null : state.Graph.Nodes[state.Ends[s, 1]];
                    Assert.That(segment.From, Is.SameAs(from));
                    Assert.That(segment.To, Is.SameAs(to));
                    Assert.That(segment.IsIsolated, Is.EqualTo(state.Ends[s, 0] < 0 && state.Ends[s, 1] < 0));
                }
                for (int n = 0; n < 3; n++)
                {
                    var expected = Enumerable.Range(0, 2).Where(s => state.Ends[s, 0] == n || state.Ends[s, 1] == n)
                        .Select(s => state.Graph.Segments[s]).ToArray();
                    Assert.That(state.Graph.Nodes[n].Segments, Is.EquivalentTo(expected));
                    Assert.That(state.Graph.Nodes[n].IsIsolated, Is.EqualTo(expected.Length == 0));
                }
            }
        }


        [Test]
        public async Task FixedNavigationTransitions()
        {
            var result = await new GraphProject().Execute(_path.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_path.Length + 1));
        }

        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public Task RandomNavigationTransitions(int seed) =>
            RandomContinuousTest.RunAsync(new GraphProject(continuous: true), seed);
    }
}
