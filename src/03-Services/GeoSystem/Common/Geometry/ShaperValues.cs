using System;
using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Geometry
{
    public static class ShaperValues
    {
        public const string TargetSegmentLength = nameof(TargetSegmentLength);
        public const float DefaultTargetSegmentLength = 1f;


        public static float GetTargetSegmentLength(IReadOnlyDictionary<string, float> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var length = values.TryGetValue(TargetSegmentLength, out var value) ? value : DefaultTargetSegmentLength;
            if (!Location.IsFinite(length) || length <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(values), "Target segment length must be finite and positive.");
            return length;
        }
    }
}
