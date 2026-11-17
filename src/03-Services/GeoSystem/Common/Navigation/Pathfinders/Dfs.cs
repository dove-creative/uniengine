using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Navigation.Pathfinders
{
    public class Dfs : IPathfinder
    {
        private class Frame
        {
            public PathSearch.Record Record { get; }
            public IEnumerator<(IReadOnlyNavSegment Segment, IReadOnlyNavNode To)> Edges { get; }


            public Frame(PathSearch.Record record)
            {
                Record = record;
                Edges = PathSearch.Neighbors(record.Node).GetEnumerator();
            }
        }


        public bool TryPathfind(IReadOnlyNavNode from, IEnumerable<IReadOnlyNavNode> targets, out Path path)
        {
            path = null;
            var goals = PathSearch.Prepare(from, targets);
            if (goals.Count == 0) return false;
            var root = new PathSearch.Record(from);
            if (goals.Contains(from))
            {
                path = PathSearch.CreatePath(root);
                return true;
            }

            var visited = new HashSet<IReadOnlyNavNode>(PathSearch.Comparer) { from };
            var pending = new Stack<Frame>();
            pending.Push(new Frame(root));
            try
            {
                while (pending.Count != 0)
                {
                    var frame = pending.Peek();
                    if (!frame.Edges.MoveNext())
                    {
                        pending.Pop().Edges.Dispose();
                        continue;
                    }

                    var edge = frame.Edges.Current;
                    if (!visited.Add(edge.To)) continue;
                    var next = new PathSearch.Record(edge.To, frame.Record, edge.Segment);
                    if (goals.Contains(next.Node))
                    {
                        path = PathSearch.CreatePath(next);
                        return true;
                    }
                    pending.Push(new Frame(next));
                }
                return false;
            }
            finally
            {
                while (pending.Count != 0) pending.Pop().Edges.Dispose();
            }
        }
    }
}
