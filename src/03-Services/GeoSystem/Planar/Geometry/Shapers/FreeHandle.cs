using System;
using System.Collections.Generic;
using System.Numerics;

namespace BlackThunder.GeoSystem.Geometry.Planar.Shapers
{
    public class FreeHandle : Shaper
    {
        protected override int RequiredHandleCount => -1;

        protected override bool TryCreatePositions(
            Vector2 from,
            Vector2 to,
            IReadOnlyList<Vector2> handles,
            float targetLength,
            List<Vector2> positions)
        {
            var start = from;
            for (int i = 0; i <= handles.Count; i++)
            {
                var end = i == handles.Count ? to : handles[i];
                var count = Math.Max(1d, Math.Ceiling(ShapeMath.Distance(start, end) / targetLength));
                if (count > MaximumPositionCount - positions.Count) return false;
                for (int step = 1; step < (int)count; step++)
                    positions.Add(ShapeMath.Lerp(start, end, step / count));
                positions.Add(end);
                start = end;
            }
            return true;
        }
    }
}
