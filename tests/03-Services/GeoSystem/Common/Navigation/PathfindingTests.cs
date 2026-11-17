using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Geometry.Pathfinders;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.Navigation.Pathfinders;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    [Category("Pathfinding")]
    public class PathfindingTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EmptySuccessAndUnreachableGoalsAreDifferent(int algorithm)
        {
            using var graph = new Graph(2);
            var search = CreateSearch(algorithm);
            var from = graph.Nodes[0];
            Assert.That(search.TryPathfind(from, new[] { from }, out var path), Is.True);
            Assert.That(path.From, Is.SameAs(from));
            Assert.That(path.Arrows, Is.Empty);
            Assert.That(search.TryPathfind(from, Array.Empty<IReadOnlyNavNode>(), out path), Is.False);
            Assert.That(path, Is.Null);
            Assert.That(search.TryPathfind(from, new[] { graph.Nodes[1] }, out path), Is.False);
            Assert.That(path, Is.Null);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TraversalOptionsControlBothDirectionsWithoutDisconnecting(int algorithm)
        {
            using var graph = new Graph(2);
            var edge = graph.Connect(0, 1);
            var search = CreateSearch(algorithm);
            foreach (var options in new[]
            {
                TraversalOptions.None,
                TraversalOptions.Forward,
                TraversalOptions.Backward,
                TraversalOptions.Both,
            })
            {
                edge.TraversalOptions = options;
                for (int direction = 0; direction < 2; direction++)
                {
                    var flag = direction == 0 ? TraversalOptions.Forward : TraversalOptions.Backward;
                    var from = graph.Nodes[direction];
                    var to = graph.Nodes[1 - direction];
                    var success = search.TryPathfind(from, new[] { to }, out var path);
                    Assert.That(success, Is.EqualTo((options & flag) != 0));
                    if (success)
                    {
                        Assert.That(path.From, Is.SameAs(from));
                        Assert.That(path.Arrows.Single().Segment, Is.SameAs(edge));
                        Assert.That(path.Arrows.Single().To, Is.SameAs(to));
                    }
                    else Assert.That(path, Is.Null);
                }
                Assert.That(graph.Nodes[0].Segments.Single(), Is.SameAs(edge));
                Assert.That(edge.Nodes.Count(), Is.EqualTo(2));
            }
        }

        [TestCase(0, 1, 30)]
        [TestCase(1, 2, 18)]
        [TestCase(2, 2, 2)]
        [TestCase(3, 2, 2)]
        public void AlgorithmsChooseTheirDocumentedPath(int algorithm, int count, int cost)
        {
            using var graph = new Graph(4);
            graph.Connect(0, 1, 9, TraversalOptions.Forward);
            graph.Connect(1, 3, 9, TraversalOptions.Forward);
            graph.Connect(0, 3, 30, TraversalOptions.Forward);
            graph.Connect(0, 2, 1, TraversalOptions.Forward);
            graph.Connect(2, 3, 1, TraversalOptions.Forward);
            var success = CreateSearch(algorithm).TryPathfind(graph.Nodes[0], new[] { graph.Nodes[3] }, out var path);
            Assert.That(success, Is.True);
            Assert.That(path.Arrows.Count, Is.EqualTo(count));
            Assert.That(PathCost(path), Is.EqualTo(cost));
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 3)]
        [TestCase(3, 3)]
        public void MultipleGoalsAreEnumeratedOnceAndChooseOneDestination(int algorithm, int destination)
        {
            using var graph = new Graph(4);
            graph.Connect(0, 1, 9);
            graph.Connect(0, 2, 1);
            graph.Connect(2, 3, 1);
            var enumerations = 0;
            Assert.That(CreateSearch(algorithm).TryPathfind(graph.Nodes[0], Targets(), out var path), Is.True);
            Assert.That(enumerations, Is.EqualTo(1));
            Assert.That(path.Arrows.Last().To, Is.SameAs(graph.Nodes[destination]));

            IEnumerable<IReadOnlyNavNode> Targets()
            {
                enumerations++;
                if (enumerations != 1) throw new InvalidOperationException("Targets were enumerated twice.");
                yield return graph.Nodes[1];
                yield return graph.Nodes[3];
                yield return graph.Nodes[3];
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EqualPathsKeepFirstParallelSegmentAndZeroCostCyclesTerminate(int algorithm)
        {
            using var graph = new Graph(4);
            var first = graph.Connect(0, 1, 0);
            graph.Connect(0, 1, 0);
            graph.Connect(1, 2, 0);
            graph.Connect(2, 0, 0);
            graph.Connect(1, 3, 1);
            var search = CreateSearch(algorithm);
            Assert.That(search.TryPathfind(graph.Nodes[0], new[] { graph.Nodes[3] }, out var path), Is.True);
            Assert.That(path.Arrows[0].Segment, Is.SameAs(first));
            Assert.That(PathCost(path), Is.EqualTo(1));
            Assert.That(path.Arrows.Count, Is.LessThanOrEqualTo(3));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void InputErrorsAreDistinctFromMissingPathsAndPartialEdgesAreSkipped(int algorithm)
        {
            using var graph = new Graph(3);
            var search = CreateSearch(algorithm);
            var from = graph.Nodes[0];
            var goal = graph.Nodes[2];
            Assert.Throws<ArgumentNullException>(() => search.TryPathfind(null, new[] { goal }, out _));
            Assert.Throws<ArgumentNullException>(() => search.TryPathfind(from, null, out _));
            Assert.Throws<ArgumentException>(() => search.TryPathfind(from, new IReadOnlyNavNode[] { null }, out _));
            var partial = graph.Connect(0, 1);
            partial.DisconnectNode(ConnectionSide.To);
            Assert.That(search.TryPathfind(from, new[] { goal }, out var path), Is.False);
            Assert.That(path, Is.Null);
            goal.Dispose();
            Assert.Throws<ArgumentException>(() => search.TryPathfind(from, new[] { goal }, out _));
            from.Dispose();
            Assert.Throws<ObjectDisposedException>(() =>
                search.TryPathfind(from, Array.Empty<IReadOnlyNavNode>(), out _));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SearchUsesReferenceIdentityAndDoesNotMutateGraphOrPayloads(int algorithm)
        {
            using var graph = new Graph(3, equalNodes: true);
            graph.Connect(0, 1);
            graph.Connect(1, 2);
            var updates = 0;
            var payload = new object();
            foreach (var node in graph.Nodes)
            {
                node.Property.Add("payload", payload);
                node.EventHub.Connect(new object()).OnUpdated(_ => updates++);
            }
            foreach (var edge in graph.Edges)
            {
                edge.Property.Add("payload", payload);
                edge.EventHub.Connect(new object()).OnUpdated(_ => updates++);
            }
            var success = CreateSearch(algorithm).TryPathfind(graph.Nodes[0], new[] { graph.Nodes[2] }, out var path);
            Assert.That(success, Is.True);
            Assert.That(path.Arrows.Count, Is.EqualTo(2));
            Assert.That(updates, Is.Zero);
            foreach (var node in graph.Nodes)
            {
                Assert.That(node.Property.Count, Is.EqualTo(1));
                Assert.That(node.Property.Get("payload"), Is.SameAs(payload));
            }
            foreach (var edge in graph.Edges)
            {
                Assert.That(edge.Property.Count, Is.EqualTo(1));
                Assert.That(edge.Nodes.Count(), Is.EqualTo(2));
                Assert.That(edge.TraversalOptions, Is.EqualTo(TraversalOptions.Both));
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        public void WeightedSearchRejectsInvalidLengthsAndAccumulatesBeyondFloatRange(int algorithm)
        {
            using var graph = new Graph(3);
            var first = graph.Connect(0, 1);
            graph.Connect(1, 2, float.MaxValue);
            var search = CreateSearch(algorithm);
            foreach (var invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                first.SetLength(invalid);
                Assert.Throws<InvalidOperationException>(() =>
                    search.TryPathfind(graph.Nodes[0], new[] { graph.Nodes[2] }, out _));
            }
            first.SetLength(float.MaxValue);
            Assert.That(search.TryPathfind(graph.Nodes[0], new[] { graph.Nodes[2] }, out var path), Is.True);
            Assert.That(PathCost(path), Is.EqualTo((double)float.MaxValue * 2));
        }

        [Test]
        public void AStarReopensNodesForAnAdmissibleInconsistentHeuristic()
        {
            using var graph = new Graph(4);
            graph.Connect(0, 1, 3, TraversalOptions.Forward);
            graph.Connect(0, 2, 1, TraversalOptions.Forward);
            graph.Connect(2, 1, 1, TraversalOptions.Forward);
            graph.Connect(1, 3, 3, TraversalOptions.Forward);
            var estimates = new[] { 0f, 0f, 4f, 0f };
            var search = new AStar<int>((position, _) => estimates[position]);
            Assert.That(search.TryPathfind(graph.Nodes[0], new[] { graph.Nodes[3] }, out var path), Is.True);
            Assert.That(path.Arrows.Select(arrow => arrow.To.Position), Is.EqualTo(new[] { 2, 1, 3 }));
            Assert.That(PathCost(path), Is.EqualTo(5));
            Path<int> typed = path;
            Path untyped = path;
            Assert.That(untyped.From, Is.SameAs(typed.From));
            Assert.That(untyped.Arrows[0], Is.SameAs(typed.Arrows[0]));
        }

        [Test]
        public void AStarValidatesHeuristicsAndTheUntypedAdapter()
        {
            using var graph = new Graph(2);
            graph.Connect(0, 1);
            Assert.Throws<ArgumentNullException>(() => new AStar<int>(null));
            foreach (var invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                var search = new AStar<int>((_, _) => invalid);
                Assert.Throws<InvalidOperationException>(() =>
                    search.TryPathfind(graph.Nodes[0], new[] { graph.Nodes[1] }, out _));
                Assert.That(search.TryPathfind(graph.Nodes[0], new[] { graph.Nodes[0] }, out var path), Is.True);
                Assert.That(path.Arrows, Is.Empty);
            }
            IPathfinder untyped = new AStar<int>((_, _) => 0);
            using var foreign = new NavNode();
            Assert.Throws<ArgumentException>(() => untyped.TryPathfind(foreign, new[] { graph.Nodes[1] }, out _));
            Assert.Throws<ArgumentException>(() => untyped.TryPathfind(graph.Nodes[0], new[] { foreign }, out _));
        }

        [Test]
        public void PathCopiesItsArrowsAndRetainsReferencesAfterSourceChanges()
        {
            using var graph = new Graph(3);
            var first = graph.Connect(0, 1);
            var second = graph.Connect(1, 2);
            var arrows = new List<Arrow<int>>
            {
                new(first, graph.Nodes[1]),
                new(second, graph.Nodes[2]),
            };
            var path = new Path<int>(graph.Nodes[0], arrows);
            arrows.Clear();
            Assert.That(path.Arrows.Count, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IList<Arrow<int>>)path.Arrows).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<Arrow>)((Path)path).Arrows).Clear());
            first.TraversalOptions = TraversalOptions.None;
            first.ClearNodes();
            second.Dispose();
            Assert.That(path.From, Is.SameAs(graph.Nodes[0]));
            Assert.That(path.Arrows[0].Segment, Is.SameAs(first));
            Assert.That(path.Arrows[0].To, Is.SameAs(graph.Nodes[1]));
            Assert.That(path.Arrows[1].Segment, Is.SameAs(second));
        }

        [Test]
        public void PathAndArrowConstructorsRejectBrokenChains()
        {
            using var graph = new Graph(3);
            var edge = graph.Connect(0, 1);
            var arrow = new Arrow(edge, graph.Nodes[1]);
            Assert.Throws<ArgumentNullException>(() => new Arrow(null, graph.Nodes[1]));
            Assert.Throws<ArgumentNullException>(() => new Arrow(edge, null));
            Assert.Throws<ArgumentException>(() => new Arrow(edge, graph.Nodes[2]));
            Assert.Throws<ArgumentNullException>(() => new Path(null, Array.Empty<Arrow>()));
            Assert.Throws<ArgumentNullException>(() => new Path(graph.Nodes[0], null));
            Assert.Throws<ArgumentException>(() => new Path(graph.Nodes[0], new Arrow[] { null }));
            Assert.Throws<ArgumentException>(() => new Path(graph.Nodes[2], new[] { arrow }));
            Assert.Throws<ArgumentException>(() => new Path(graph.Nodes[1], new[] { arrow }));
            edge.ClearNodes();
            Assert.Throws<ArgumentException>(() => new Path(graph.Nodes[0], new[] { arrow }));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SeededDirectedGraphsMatchIndependentShortestDistances(int seed)
        {
            var random = new Random(seed);
            for (int scenario = 0; scenario < 20; scenario++)
            {
                using var graph = new Graph(6);
                var distances = new double[6, 6];
                for (int i = 0; i < 6; i++)
                    for (int j = 0; j < 6; j++) distances[i, j] = i == j ? 0 : double.PositiveInfinity;
                for (int i = 0; i < 6; i++)
                {
                    for (int j = i + 1; j < 6; j++)
                    {
                        if (random.Next(3) == 0) continue;
                        var weight = random.Next(10);
                        var options = (TraversalOptions)random.Next(4);
                        graph.Connect(i, j, weight, options);
                        if ((options & TraversalOptions.Forward) != 0) distances[i, j] = weight;
                        if ((options & TraversalOptions.Backward) != 0) distances[j, i] = weight;
                    }
                }
                for (int k = 0; k < 6; k++)
                    for (int i = 0; i < 6; i++)
                        for (int j = 0; j < 6; j++)
                            distances[i, j] = Math.Min(distances[i, j], distances[i, k] + distances[k, j]);

                var scale = Enumerable.Range(0, 6).Select(_ => random.NextDouble()).ToArray();
                IPathfinder[] searches =
                {
                    new Dijkstra(),
                    new AStar<int>((_, _) => 0f),
                    new AStar<int>((a, b) => double.IsInfinity(distances[a, b])
                        ? 0f : (float)(distances[a, b] * scale[a] * 0.5)),
                };
                foreach (var search in searches)
                {
                    for (int source = 0; source < 6; source++)
                    {
                        var expected = Math.Min(distances[source, 4], distances[source, 5]);
                        var success = search.TryPathfind(
                            graph.Nodes[source], new[] { graph.Nodes[4], graph.Nodes[5] }, out var path);
                        Assert.That(success, Is.EqualTo(!double.IsInfinity(expected)), $"seed={seed}, case={scenario}");
                        if (!success)
                        {
                            Assert.That(path, Is.Null);
                            continue;
                        }
                        Assert.That(PathCost(path), Is.EqualTo(expected), $"seed={seed}, case={scenario}");
                        var destination = path.Arrows.Count == 0 ? path.From : path.Arrows.Last().To;
                        Assert.That(ReferenceEquals(destination, graph.Nodes[4])
                            || ReferenceEquals(destination, graph.Nodes[5]), Is.True);
                    }
                }
            }
        }

        [Test]
        public void DepthFirstSearchHandlesADeepChainWithoutRecursion()
        {
            using var graph = new Graph(10000);
            for (int i = 1; i < graph.Nodes.Count; i++) graph.Connect(i - 1, i, 1, TraversalOptions.Forward);
            Assert.That(new Dfs().TryPathfind(graph.Nodes[0], new[] { graph.Nodes.Last() }, out var path), Is.True);
            Assert.That(path.Arrows.Count, Is.EqualTo(9999));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ReadOnlyGraphsSkipDisposedIncompleteAndUnrelatedEdges(int algorithm)
        {
            using var from = new ReadOnlyNode();
            using var via = new ReadOnlyNode();
            using var goal = new ReadOnlyNode();
            using var disposed = new ReadOnlyNode();
            disposed.Dispose();
            using var deadEdge = new ReadOnlySegment(from, goal);
            deadEdge.Dispose();
            using var missing = new ReadOnlySegment(from, null);
            using var deadEnd = new ReadOnlySegment(from, disposed);
            using var first = new ReadOnlySegment(from, via);
            using var last = new ReadOnlySegment(via, goal);
            from.Edges.AddRange(new[] { deadEdge, missing, deadEnd, last, first });
            via.Edges.Add(last);
            Assert.That(CreateSearch(algorithm).TryPathfind(from, new[] { goal }, out var path), Is.True);
            Assert.That(path.Arrows.Select(arrow => arrow.Segment), Is.EqualTo(new[] { first, last }));
        }

        private class ReadOnlySubject : IEventSubject<int>, IPropertySubject
        {
            public bool IsDisposed { get; private set; }
            public IEventHub<int> EventHub => throw new InvalidOperationException("Search must not use events.");
            public Property Property => throw new InvalidOperationException("Search must not use metadata.");
            public string FormatLogMessage(string message) => message;
            public void Dispose() => IsDisposed = true;
        }

        private class ReadOnlyNode : ReadOnlySubject, IReadOnlyNode<int>
        {
            public int Position => 0;
            public List<IReadOnlySegment<int>> Edges { get; } = new();
            public IEnumerable<IReadOnlySegment<int>> Segments => Edges;
            public bool IsIsolated => Edges.Count == 0;
        }

        private class ReadOnlySegment : ReadOnlySubject, IReadOnlySegment<int>
        {
            public IReadOnlyNode<int> From { get; }
            public IReadOnlyNode<int> To { get; }
            public IEnumerable<IReadOnlyNode<int>> Nodes => new[] { From, To }.Where(node => node != null);
            public bool IsIsolated => From == null && To == null;
            public float Length => IsDisposed ? throw new ObjectDisposedException(nameof(ReadOnlySegment)) : 1f;
            public TraversalOptions TraversalOptions => TraversalOptions.Both;


            public ReadOnlySegment(IReadOnlyNode<int> from, IReadOnlyNode<int> to)
            {
                From = from;
                To = to;
            }

            public bool IsFromNode(IReadOnlyNavNode node) => ReferenceEquals(node, From);
            public IReadOnlyNavNode GetCounterNode(IReadOnlyNavNode node) => IsFromNode(node) ? To : From;
            public bool TryGetPosition(Location location, out int position, float? epsilon = null)
            {
                position = 0;
                return false;
            }
        }

        private static IPathfinder CreateSearch(int algorithm) => algorithm switch
        {
            0 => new Bfs(),
            1 => new Dfs(),
            2 => new Dijkstra(),
            3 => new AStar<int>((_, _) => 0f),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
        };

        private static double PathCost(Path path) => path.Arrows.Sum(arrow => (double)arrow.Segment.Length);

        private class EqualNode : Node<int>
        {
            public EqualNode(int position) : base(position) { }
            public override bool Equals(object other) => other is EqualNode;
            public override int GetHashCode() => 0;
        }

        private class Graph : IDisposable
        {
            public List<Node<int>> Nodes { get; } = new();
            public List<TestSegment> Edges { get; } = new();


            public Graph(int count, bool equalNodes = false)
            {
                for (int i = 0; i < count; i++) Nodes.Add(equalNodes ? new EqualNode(i) : new Node<int>(i));
            }

            public TestSegment Connect(
                int from,
                int to,
                float length = 1,
                TraversalOptions options = TraversalOptions.Both)
            {
                var segment = new TestSegment(length) { TraversalOptions = options };
                Edges.Add(segment);
                segment.ConnectNode(ConnectionSide.From, Nodes[from]);
                segment.ConnectNode(ConnectionSide.To, Nodes[to]);
                return segment;
            }

            public void Dispose()
            {
                foreach (var segment in Edges) segment.Dispose();
                foreach (var node in Nodes) node.Dispose();
            }
        }

        private class TestSegment : Segment<int, int>
        {
            public TestSegment(float length) => Length = length;
            public void SetLength(float length) => Length = length;

            public override bool TryGetTransform(
                Location location,
                out int position,
                out int rotation,
                float? epsilon = null)
            {
                position = default;
                rotation = default;
                return false;
            }
        }
    }
}
