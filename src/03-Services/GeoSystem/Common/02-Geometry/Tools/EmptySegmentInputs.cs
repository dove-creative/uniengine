using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BlackThunder.GeoSystem.Geometry
{
    internal static class EmptySegmentInputs<TPosition>
    {
        public static IDictionary<string, float> Values { get; }
            = new ReadOnlyDictionary<string, float>(new Dictionary<string, float>());
        public static IList<ControlHandle<TPosition>> Handles { get; }
            = new List<ControlHandle<TPosition>>().AsReadOnly();
    }
}
