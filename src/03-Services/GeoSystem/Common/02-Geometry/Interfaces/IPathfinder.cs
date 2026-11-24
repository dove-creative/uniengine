using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Geometry
{
    public interface IPathfinder<TPosition>
    {
        bool TryPathfind(
            IReadOnlyNode<TPosition> from,
            IEnumerable<IReadOnlyNode<TPosition>> targets,
            out Path<TPosition> path);
    }
}
