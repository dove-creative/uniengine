using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Mathematics;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    public abstract class Shaper : IShaper<Vector2>
    {
        // Bound work for extremely small target lengths and unreliably representable subdivisions.
        public const int MaximumPositionCount = 65536;
        protected abstract int RequiredHandleCount { get; }


        public bool TryShape(
            Vector2 from,
            Vector2 to,
            IReadOnlyDictionary<string, float> values,
            IReadOnlyList<Vector2> handles,
            ICollection<Vector2> positions)
        {
            if (handles == null) throw new ArgumentNullException(nameof(handles));
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            var targetLength = ShaperValues.GetTargetSegmentLength(values);
            if (!UniMath.IsFinite(from) || !UniMath.IsFinite(to)) return false;
            if (RequiredHandleCount >= 0 && handles.Count != RequiredHandleCount) return false;
            if (handles.Count > MaximumPositionCount - 2) return false;
            foreach (var handle in handles)
                if (!UniMath.IsFinite(handle)) return false;

            var generated = new List<Vector2> { from };
            if (!TryCreatePositions(from, to, handles, targetLength, generated)) return false;
            foreach (var position in generated) positions.Add(position);
            return true;
        }

        protected abstract bool TryCreatePositions(
            Vector2 from,
            Vector2 to,
            IReadOnlyList<Vector2> handles,
            float targetLength,
            List<Vector2> positions);
    }
}
