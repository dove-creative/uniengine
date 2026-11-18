using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BlackThunder.GeoSystem.Navigation
{
    internal static class PathSearch
    {
        public static IEqualityComparer<IReadOnlyNavNode> Comparer { get; } = new NodeComparer();

        public class Record
        {
            public IReadOnlyNavNode Node { get; }
            public Record Previous { get; }
            public IReadOnlyNavSegment Segment { get; }
            public double Cost { get; }


            public Record(
                IReadOnlyNavNode node,
                Record previous = null,
                IReadOnlyNavSegment segment = null,
                double cost = 0)
            {
                Node = node;
                Previous = previous;
                Segment = segment;
                Cost = cost;
            }
        }

        private class NodeComparer : IEqualityComparer<IReadOnlyNavNode>
        {
            public bool Equals(IReadOnlyNavNode x, IReadOnlyNavNode y) => ReferenceEquals(x, y);
            public int GetHashCode(IReadOnlyNavNode obj) => RuntimeHelpers.GetHashCode(obj);
        }


        public static HashSet<IReadOnlyNavNode> Prepare(
            IReadOnlyNavNode from,
            IEnumerable<IReadOnlyNavNode> targets)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (from.IsDisposed) throw new ObjectDisposedException(nameof(from));
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            var goals = new HashSet<IReadOnlyNavNode>(Comparer);
            foreach (var target in targets)
            {
                if (target == null || target.IsDisposed)
                    throw new ArgumentException("Every target must be a live node.", nameof(targets));
                goals.Add(target);
            }
            return goals;
        }

        public static IEnumerable<(IReadOnlyNavSegment Segment, IReadOnlyNavNode To)> Neighbors(IReadOnlyNavNode node)
        {
            if (node.IsDisposed) yield break;
            foreach (var segment in node.Segments)
            {
                if (segment == null || segment.IsDisposed) continue;
                var from = segment.From;
                var to = segment.To;
                if (from == null || to == null || from.IsDisposed || to.IsDisposed || ReferenceEquals(from, to))
                    continue;

                if (ReferenceEquals(node, from) && (segment.TraversalOptions & TraversalOptions.Forward) != 0)
                    yield return (segment, to);
                else if (ReferenceEquals(node, to) && (segment.TraversalOptions & TraversalOptions.Backward) != 0)
                    yield return (segment, from);
            }
        }

        public static double GetCost(IReadOnlyNavSegment segment)
        {
            var length = segment.Length;
            if (float.IsNaN(length) || float.IsInfinity(length) || length < 0f)
                throw new InvalidOperationException("A traversable segment must have a finite, nonnegative length.");
            return length;
        }

        public static Path CreatePath(Record last)
        {
            var arrows = new List<Arrow>();
            var current = last;
            while (current.Previous != null)
            {
                arrows.Add(new Arrow(current.Segment, current.Node));
                current = current.Previous;
            }
            arrows.Reverse();
            return new Path(current.Node, arrows);
        }
    }
}
