using System;
using System.Numerics;

namespace BlackThunder.UniEngine.Mathematics
{
    public static partial class UniMath
    {
        // Content --------------------
        // Snapping
        public static Vector2 Snap(this Vector2 value, float unit) => value.Snap(new Vector2(unit));
        public static Vector2 Snap(this Vector2 value, Vector2 unit) =>
            new(value.X.Snap(unit.X), value.Y.Snap(unit.Y));
        public static Vector3 Snap(this Vector3 value, float unit) => value.Snap(new Vector3(unit));
        public static Vector3 Snap(this Vector3 value, Vector3 unit) =>
            new(value.X.Snap(unit.X), value.Y.Snap(unit.Y), value.Z.Snap(unit.Z));

        // Vector comparisons
        public static bool Is(this Vector2 value, Vector2 other, float epsilon = Epsilon)
        {
            ValidateTolerance(epsilon, nameof(epsilon));
            if (!IsFinite(value) || !IsFinite(other)) return false;

            var x = (double)value.X - other.X;
            var y = (double)value.Y - other.Y;
            return x * x + y * y <= (double)epsilon * epsilon;
        }

        public static bool IsZero(this Vector2 value, float epsilon = Epsilon) => value.Is(Vector2.Zero, epsilon);

        public static bool Is(this Vector3 value, Vector3 other, float epsilon = Epsilon)
        {
            ValidateTolerance(epsilon, nameof(epsilon));
            if (!IsFinite(value) || !IsFinite(other)) return false;

            var x = (double)value.X - other.X;
            var y = (double)value.Y - other.Y;
            var z = (double)value.Z - other.Z;
            return x * x + y * y + z * z <= (double)epsilon * epsilon;
        }

        public static bool IsZero(this Vector3 value, float epsilon = Epsilon) => value.Is(Vector3.Zero, epsilon);

        public static bool Is(this Vector4 value, Vector4 other, float epsilon = Epsilon)
        {
            ValidateTolerance(epsilon, nameof(epsilon));
            if (!IsFinite(value) || !IsFinite(other)) return false;

            var x = (double)value.X - other.X;
            var y = (double)value.Y - other.Y;
            var z = (double)value.Z - other.Z;
            var w = (double)value.W - other.W;
            return x * x + y * y + z * z + w * w <= (double)epsilon * epsilon;
        }

        public static bool IsZero(this Vector4 value, float epsilon = Epsilon) => value.Is(Vector4.Zero, epsilon);

        public static bool IsParallelWith(
            this Vector2 value, Vector2 other, out bool isSameDirection, float epsilon = Epsilon)
        {
            ValidateTolerance(epsilon, nameof(epsilon));
            var first = NormalizeVector(value, nameof(value));
            var second = NormalizeVector(other, nameof(other));
            var dot = first.X * second.X + first.Y * second.Y;
            var parallel = System.Math.Abs(System.Math.Abs(dot) - 1d) <= epsilon;
            isSameDirection = parallel && dot > 0d;
            return parallel;
        }

        // Planar calculations
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

        // Validation and normalization
        public static bool IsFinite(Vector2 value) => IsFinite(value.X) && IsFinite(value.Y);
        public static bool IsFinite(Vector3 value) => IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        public static bool IsFinite(Vector4 value) =>
            IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z) && IsFinite(value.W);

        private static (double X, double Y) NormalizeVector(Vector2 value, string parameterName)
        {
            var length = System.Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y);
            if (!(length > 0d) || double.IsInfinity(length))
                throw new ArgumentException("A direction must be finite and nonzero.", parameterName);

            return (value.X / length, value.Y / length);
        }
    }
}
