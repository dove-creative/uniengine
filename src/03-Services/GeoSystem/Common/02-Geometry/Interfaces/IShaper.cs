using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Geometry
{
    public interface IShaper<TPosition>
    {
        // Append an ordered position sequence including both endpoints; false means no usable shape.
        bool TryShape(
            TPosition from,
            TPosition to,
            IReadOnlyDictionary<string, float> values,
            IReadOnlyList<TPosition> handles,
            ICollection<TPosition> positions);
    }
}
