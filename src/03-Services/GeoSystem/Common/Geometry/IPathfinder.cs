using System;
using System.Collections.Generic;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public interface IPathfinder<P> : IPathfinder
    {
        bool TryPathfind(IReadOnlyNode<P> from, IEnumerable<IReadOnlyNode<P>> targets, out Path<P> path);

        bool IPathfinder.TryPathfind(
            IReadOnlyNavNode from,
            IEnumerable<IReadOnlyNavNode> targets,
            out Navigation.Path path)
        {
            path = null;
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (from is not IReadOnlyNode<P> typedFrom)
                throw new ArgumentException("The source must use the pathfinder's position type.", nameof(from));

            var typedTargets = new List<IReadOnlyNode<P>>();
            foreach (var target in targets)
            {
                if (target is not IReadOnlyNode<P> typedTarget)
                    throw new ArgumentException(
                        "All targets must use the pathfinder's position type.", nameof(targets));
                typedTargets.Add(typedTarget);
            }

            var success = TryPathfind(typedFrom, typedTargets, out var typedPath);
            path = typedPath;
            return success;
        }
    }
}
