using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BlackThunder.GeoSystem.Geometry
{
    internal static class EmptySegmentInputs<P>
    {
        public static IDictionary<string, float> Values { get; }
            = new ReadOnlyDictionary<string, float>(new Dictionary<string, float>());
        public static IList<SegmentHandle<P>> Handles { get; }
            = new List<SegmentHandle<P>>().AsReadOnly();
    }
}
