using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Navigation
{
    public interface IPathfinder
    {
        bool TryPathfind(IReadOnlyNavNode from, IEnumerable<IReadOnlyNavNode> targets, out Path path);
    }
}
