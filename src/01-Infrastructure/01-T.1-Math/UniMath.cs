using System;
using System.Numerics;

namespace BlackThunder.UniEngine.Mathematics
{
    public static partial class UniMath
    {
        // Front
        public const float Epsilon = 0.00001f;
        public const float AngularEpsilon = 0.00001f; // Degrees


        // Content --------------------
        // Numeric comparisons
        public static bool Is(this float value, float other, float epsilon = Epsilon)
        {
            ValidateTolerance(epsilon, nameof(epsilon));
            return IsFinite(value) && IsFinite(other) && System.Math.Abs((double)value - other) <= epsilon;
        }

        public static bool IsZero(this float value, float epsilon = Epsilon) => value.Is(0f, epsilon);

        public static bool IsPositive(this float value, bool tolerant, float epsilon = Epsilon)
        {
            ValidateTolerance(epsilon, nameof(epsilon));
            return IsFinite(value) && value > (tolerant ? -epsilon : epsilon);
        }

        public static bool IsNegative(this float value, bool tolerant, float epsilon = Epsilon)
        {
            ValidateTolerance(epsilon, nameof(epsilon));
            return IsFinite(value) && value < (tolerant ? epsilon : -epsilon);
        }

        public static bool IsMoreThan(this float value, float criterion, bool tolerant, float epsilon = Epsilon)
        {
            ValidateTolerance(epsilon, nameof(epsilon));
            return IsFinite(value) && IsFinite(criterion)
                && (double)value + (tolerant ? epsilon : -epsilon) >= criterion;
        }

        public static bool IsLessThan(this float value, float criterion, bool tolerant, float epsilon = Epsilon)
        {
            ValidateTolerance(epsilon, nameof(epsilon));
            return IsFinite(value) && IsFinite(criterion)
                && value <= (double)criterion + (tolerant ? epsilon : -epsilon);
        }

        // Snapping
        public static float Snap(this float value, float unit)
        {
            if (!IsFinite(unit) || unit <= 0f)
                throw new ArgumentOutOfRangeException(nameof(unit), unit, "The unit must be finite and positive.");

            return (float)(System.Math.Round((double)value / unit, MidpointRounding.ToEven) * unit);
        }

        public static float SnapTo(this float value, float criterion, out bool isSnapped) =>
            value.SnapTo(criterion, Epsilon, out isSnapped);
        public static float SnapTo(this float value, float criterion, float epsilon, out bool isSnapped)
        {
            isSnapped = value.Is(criterion, epsilon);
            return isSnapped ? criterion : value;
        }

        // Rotation comparisons (degrees)
        public static bool IsSameRotationAs(
            this Quaternion value, Quaternion other, float angularTolerance = AngularEpsilon)
        {
            ValidateTolerance(angularTolerance, nameof(angularTolerance));
            return RotationDifference(value, other) <= angularTolerance;
        }

        public static bool IsHalfTurnFrom(
            this Quaternion value, Quaternion other, float angularTolerance = AngularEpsilon)
        {
            ValidateTolerance(angularTolerance, nameof(angularTolerance));
            return System.Math.Abs(180d - RotationDifference(value, other)) <= angularTolerance;
        }

        // Shared numerical operations
        private static void ValidateTolerance(float tolerance, string parameterName)
        {
            if (!IsFinite(tolerance) || tolerance < 0f)
                throw new ArgumentOutOfRangeException(
                    parameterName, tolerance, "The tolerance must be finite and nonnegative.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static (double X, double Y, double Z, double W) NormalizeRotation(
            Quaternion value, string parameterName)
        {
            var length = System.Math.Sqrt(
                (double)value.X * value.X + (double)value.Y * value.Y
                + (double)value.Z * value.Z + (double)value.W * value.W);
            if (!(length > 0d) || double.IsInfinity(length))
                throw new ArgumentException("A rotation must be finite and nonzero.", parameterName);

            return (value.X / length, value.Y / length, value.Z / length, value.W / length);
        }

        private static double RotationDifference(Quaternion value, Quaternion other)
        {
            var first = NormalizeRotation(value, nameof(value));
            var second = NormalizeRotation(other, nameof(other));
            var dot = first.X * second.X + first.Y * second.Y + first.Z * second.Z + first.W * second.W;
            if (dot < 0d)
                second = (-second.X, -second.Y, -second.Z, -second.W);

            // Quaternion signs encode the same rotation; the aligned chord retains small angles.
            var dx = first.X - second.X;
            var dy = first.Y - second.Y;
            var dz = first.Z - second.Z;
            var dw = first.W - second.W;
            var sx = first.X + second.X;
            var sy = first.Y + second.Y;
            var sz = first.Z + second.Z;
            var sw = first.W + second.W;
            var difference = System.Math.Sqrt(dx * dx + dy * dy + dz * dz + dw * dw);
            var sum = System.Math.Sqrt(sx * sx + sy * sy + sz * sz + sw * sw);
            var angle = 4d * System.Math.Atan2(difference, sum) * (180d / System.Math.PI);
            return System.Math.Min(180d, angle);
        }
    }
}
