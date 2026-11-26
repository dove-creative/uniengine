using System;
using System.Numerics;
using BlackThunder.UniEngine.Mathematics;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    internal static class LocationProjection
    {
        public static void AddLine(
            ref LocationQuery query,
            Vector2 position,
            Vector2 from,
            Vector2 to,
            double fromDistance,
            double toDistance)
        {
            var low = Math.Max(query.MinDistance, fromDistance);
            var high = Math.Min(query.MaxDistance, toDistance);
            if (low > high || toDistance <= fromDistance) return;

            var x = (double)to.X - from.X;
            var y = (double)to.Y - from.Y;
            var squared = x * x + y * y;
            if (squared == 0) return;

            var rate = (((double)position.X - from.X) * x + ((double)position.Y - from.Y) * y) / squared;
            var length = toDistance - fromDistance;
            rate = Math.Clamp(rate, (low - fromDistance) / length, (high - fromDistance) / length);
            var distance = Math.Clamp(fromDistance + rate * length, low, high);
            var projected = UniMath.Lerp(from, to, rate);
            var errorX = (double)position.X - projected.X;
            var errorY = (double)position.Y - projected.Y;
            query.Consider(distance, errorX * errorX + errorY * errorY);
        }
    }
}
