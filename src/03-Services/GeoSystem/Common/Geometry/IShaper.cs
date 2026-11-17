using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Geometry
{
    public interface IShaper<P>
    {
        // Append an ordered position sequence including both endpoints; false means no usable shape.
        bool TryShape(
            P from,
            P to,
            IReadOnlyDictionary<string, float> values,
            IReadOnlyList<P> handles,
            ICollection<P> positions);
    }
}
