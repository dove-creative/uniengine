using System;
using System.Numerics;

namespace BlackThunder.GeoSystem.Geometry.Planar
{
    internal static class ShapeMath
    {
        public static bool IsFinite(Vector2 point) => Location.IsFinite(point.X) && Location.IsFinite(point.Y);

        public static double Distance(Vector2 from, Vector2 to)
        {
            var x = (double)to.X - from.X;
            var y = (double)to.Y - from.Y;
            return Math.Sqrt(x * x + y * y);
        }

        public static Vector2 Lerp(Vector2 from, Vector2 to, double rate) =>
            new((float)(from.X + ((double)to.X - from.X) * rate),
                (float)(from.Y + ((double)to.Y - from.Y) * rate));

        public static float Rotation(Vector2 from, Vector2 to) =>
            (float)(Math.Atan2((double)to.Y - from.Y, (double)to.X - from.X) * 180d / Math.PI);
    }
}
