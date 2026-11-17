using System.Collections.Generic;
using System.Numerics;

namespace BlackThunder.GeoSystem.Geometry.Planar.Shapers
{
    internal static class BezierSampling
    {
        public static bool TryCreate(Vector2[] controlPoints, float targetLength, List<Vector2> positions)
        {
            var pending = new Stack<(Vector2[] Points, int Depth)>();
            pending.Push((controlPoints, 0));
            while (pending.Count != 0)
            {
                var (points, depth) = pending.Pop();
                double polygonLength = 0;
                for (int i = 1; i < points.Length; i++)
                    polygonLength += ShapeMath.Distance(points[i - 1], points[i]);
                if (polygonLength <= targetLength)
                {
                    if (positions.Count >= Shaper.MaximumPositionCount) return false;
                    positions.Add(points[points.Length - 1]);
                    continue;
                }
                if (depth >= 32) return false;

                var left = new Vector2[points.Length];
                var right = new Vector2[points.Length];
                var work = (Vector2[])points.Clone();
                left[0] = points[0];
                right[right.Length - 1] = points[points.Length - 1];
                for (int level = 1; level < points.Length; level++)
                {
                    for (int i = 0; i < points.Length - level; i++)
                        work[i] = ShapeMath.Lerp(work[i], work[i + 1], 0.5d);
                    left[level] = work[0];
                    right[right.Length - level - 1] = work[points.Length - level - 1];
                }
                pending.Push((right, depth + 1));
                pending.Push((left, depth + 1));
            }
            return true;
        }
    }
}
