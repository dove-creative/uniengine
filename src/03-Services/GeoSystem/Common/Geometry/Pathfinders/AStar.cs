using System;
using System.Collections.Generic;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry.Pathfinders
{
    /// <summary>
    /// Finds a least-cost path when the supplied heuristic never overestimates the remaining cost.
    /// Reopens nodes when a lower cost is found, including with inconsistent admissible heuristics.
    /// </summary>
    public class AStar<P> : IPathfinder<P>
    {
        private readonly Func<P, P, float> _estimate;


        public AStar(Func<P, P, float> estimate) =>
            _estimate = estimate ?? throw new ArgumentNullException(nameof(estimate));

        public bool TryPathfind(IReadOnlyNode<P> from, IEnumerable<IReadOnlyNode<P>> targets, out Path<P> path)
        {
            path = null;
            var goals = PathSearch.Prepare(from, targets);
            if (goals.Count == 0) return false;

            var root = new PathSearch.Record(from);
            var best = new Dictionary<IReadOnlyNavNode, PathSearch.Record>(PathSearch.Comparer) { [from] = root };
            var pending = new MinHeap<PathSearch.Record>();
            pending.Enqueue(root, Estimate(from));
            while (pending.Count != 0)
            {
                var current = pending.Dequeue();
                if (!ReferenceEquals(best[current.Node], current)) continue;
                if (goals.Contains(current.Node))
                {
                    var arrows = new List<Arrow<P>>();
                    while (current.Previous != null)
                    {
                        arrows.Add(new Arrow<P>((IReadOnlySegment<P>)current.Segment, (IReadOnlyNode<P>)current.Node));
                        current = current.Previous;
                    }
                    arrows.Reverse();
                    path = new Path<P>(from, arrows);
                    return true;
                }

                foreach (var edge in PathSearch.Neighbors(current.Node))
                {
                    if (edge.Segment is not IReadOnlySegment<P> || edge.To is not IReadOnlyNode<P> node)
                        throw new InvalidOperationException("The graph must use the pathfinder's position type.");
                    var cost = current.Cost + PathSearch.GetCost(edge.Segment);
                    if (best.TryGetValue(node, out var previous) && cost >= previous.Cost) continue;

                    var next = new PathSearch.Record(node, current, edge.Segment, cost);
                    best[node] = next;
                    pending.Enqueue(next, cost + Estimate(node));
                }
            }
            return false;

            double Estimate(IReadOnlyNode<P> node)
            {
                if (goals.Contains(node)) return 0;
                var minimum = double.PositiveInfinity;
                foreach (IReadOnlyNode<P> goal in goals)
                {
                    var estimate = _estimate(node.Position, goal.Position);
                    if (float.IsNaN(estimate) || float.IsInfinity(estimate) || estimate < 0f)
                        throw new InvalidOperationException(
                            "The heuristic must return a finite, nonnegative estimate.");
                    minimum = Math.Min(minimum, estimate);
                }
                return minimum;
            }
        }
    }
}
