using System;
using System.Collections.Generic;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public class PathfinderAdapter<TPosition> : IPathfinder<TPosition>
    {
        private readonly IPathfinder _pathfinder;


        public PathfinderAdapter(IPathfinder pathfinder) =>
            _pathfinder = pathfinder ?? throw new ArgumentNullException(nameof(pathfinder));

        public bool TryPathfind(
            IReadOnlyNode<TPosition> from,
            IEnumerable<IReadOnlyNode<TPosition>> targets,
            out Path<TPosition> path)
        {
            path = null;
            if (!_pathfinder.TryPathfind(from, targets, out var result)) return false;
            if (result == null || !ReferenceEquals(result.From, from))
                throw new InvalidOperationException("The pathfinder returned an invalid source.");

            if (result is Path<TPosition> typed)
            {
                path = typed;
                return true;
            }

            var arrows = new List<Arrow<TPosition>>(result.Arrows.Count);
            foreach (var arrow in result.Arrows)
            {
                if (arrow.Segment is not IReadOnlySegment<TPosition> segment
                    || arrow.To is not IReadOnlyNode<TPosition> to)
                    throw new InvalidOperationException("The path must use the requested position type.");
                arrows.Add(new Arrow<TPosition>(segment, to));
            }
            path = new Path<TPosition>(from, arrows);
            return true;
        }
    }
}
