using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Navigation.Pathfinders
{
    public class Dijkstra : IPathfinder
    {
        public bool TryPathfind(IReadOnlyNavNode from, IEnumerable<IReadOnlyNavNode> targets, out Path path)
        {
            path = null;
            var goals = PathSearch.Prepare(from, targets);
            if (goals.Count == 0) return false;

            var root = new PathSearch.Record(from);
            var best = new Dictionary<IReadOnlyNavNode, PathSearch.Record>(PathSearch.Comparer) { [from] = root };
            var pending = new MinHeap<PathSearch.Record>();
            pending.Enqueue(root, 0);
            while (pending.Count != 0)
            {
                var current = pending.Dequeue();
                if (!ReferenceEquals(best[current.Node], current)) continue;
                if (goals.Contains(current.Node))
                {
                    path = PathSearch.CreatePath(current);
                    return true;
                }

                foreach (var edge in PathSearch.Neighbors(current.Node))
                {
                    var cost = current.Cost + PathSearch.GetCost(edge.Segment);
                    if (best.TryGetValue(edge.To, out var previous) && cost >= previous.Cost) continue;
                    var next = new PathSearch.Record(edge.To, current, edge.Segment, cost);
                    best[edge.To] = next;
                    pending.Enqueue(next, cost);
                }
            }
            return false;
        }
    }
}
