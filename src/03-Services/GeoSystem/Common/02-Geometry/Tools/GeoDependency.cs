using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.GeoSystem.RouteSystem;

namespace BlackThunder.GeoSystem.Geometry
{
    internal static class GeoDependency
    {
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }


        public static void Validate(object owner, object source)
        {
            var pending = new Stack<object>();
            var visited = new HashSet<object>(new ReferenceComparer());
            pending.Push(source);
            while (pending.Count != 0)
            {
                var candidate = pending.Pop();
                if (ReferenceEquals(candidate, owner))
                    throw new ArgumentException("A geometric dependency cannot contain a cycle.", nameof(source));
                if (candidate == null || !visited.Add(candidate)) continue;

                if (candidate is INodePin nodePin) pending.Push(nodePin.AnchorNode);
                else if (candidate is ISegmentPin segmentPin) pending.Push(segmentPin.AnchorSegment);
                else if (candidate is IHold hold) pending.Push(hold.Anchor);
                else if (candidate is IRoute route)
                {
                    foreach (var view in route.Views) pending.Push(view.Source);
                    pending.Push(route.From);
                    pending.Push(route.To);
                }
                else if (candidate is Geometry.IReadOnlySegment segment)
                {
                    pending.Push(segment.From);
                    pending.Push(segment.To);
                }
            }
        }
    }
}
