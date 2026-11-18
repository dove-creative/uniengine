using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Navigation
{
    public class BfsPathfinder : IPathfinder
    {
        public bool TryPathfind(IReadOnlyNavNode from, IEnumerable<IReadOnlyNavNode> targets, out Path path)
        {
            path = null;
            var goals = PathSearch.Prepare(from, targets);
            if (goals.Count == 0) return false;

            var visited = new HashSet<IReadOnlyNavNode>(PathSearch.Comparer) { from };
            var pending = new Queue<PathSearch.Record>();
            pending.Enqueue(new PathSearch.Record(from));
            while (pending.Count != 0)
            {
                var current = pending.Dequeue();
                if (goals.Contains(current.Node))
                {
                    path = PathSearch.CreatePath(current);
                    return true;
                }

                foreach (var edge in PathSearch.Neighbors(current.Node))
                {
                    if (!visited.Add(edge.To)) continue;
                    pending.Enqueue(new PathSearch.Record(edge.To, current, edge.Segment));
                }
            }
            return false;
        }
    }
}
