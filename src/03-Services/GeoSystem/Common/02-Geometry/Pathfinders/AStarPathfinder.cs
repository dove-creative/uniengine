using System;
using System.Collections.Generic;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    /// <summary>
    /// Finds a least-cost path when the supplied heuristic never overestimates the remaining cost.
    /// Reopens nodes when a lower cost is found, including with inconsistent admissible heuristics.
    /// </summary>
    public class AStarPathfinder<TPosition> : IPathfinder<TPosition>
    {
        private readonly Func<TPosition, TPosition, float> _estimate;


        public AStarPathfinder(Func<TPosition, TPosition, float> estimate) =>
            _estimate = estimate ?? throw new ArgumentNullException(nameof(estimate));

        public bool TryPathfind(
            IReadOnlyNode<TPosition> from,
            IEnumerable<IReadOnlyNode<TPosition>> targets,
            out Path<TPosition> path)
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
                    var arrows = new List<Arrow<TPosition>>();
                    while (current.Previous != null)
                    {
                        arrows.Add(new Arrow<TPosition>(
                            (IReadOnlySegment<TPosition>)current.Segment, (IReadOnlyNode<TPosition>)current.Node));
                        current = current.Previous;
                    }
                    arrows.Reverse();
                    path = new Path<TPosition>(from, arrows);
                    return true;
                }

                foreach (var edge in PathSearch.Neighbors(current.Node))
                {
                    if (edge.Segment is not IReadOnlySegment<TPosition> || edge.To is not IReadOnlyNode<TPosition> node)
                        throw new InvalidOperationException("The graph must use the pathfinder's position type.");
                    var cost = current.Cost + PathSearch.GetCost(edge.Segment);
                    if (best.TryGetValue(node, out var previous) && cost >= previous.Cost) continue;

                    var next = new PathSearch.Record(node, current, edge.Segment, cost);
                    best[node] = next;
                    pending.Enqueue(next, cost + Estimate(node));
                }
            }
            return false;

            double Estimate(IReadOnlyNode<TPosition> node)
            {
                if (goals.Contains(node)) return 0;
                var minimum = double.PositiveInfinity;
                foreach (IReadOnlyNode<TPosition> goal in goals)
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
