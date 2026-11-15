using System;
using System.Globalization;
using System.Numerics;
using BlackThunder.UniEngine.Mathematics;

namespace BlackThunder.UniEngine.Objects
{
    public struct Transform2D : IEquatable<Transform2D>
    {
        // Front
        public Vector2 Position { get; set; }
        public float Rotation { get; set; }

        public static Transform2D Identity => new(Vector2.Zero, 0f);


        // Content
        public Transform2D(Vector2 position, float rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public readonly bool Is(
            Transform2D other,
            float positionTolerance = UniMath.Epsilon,
            float angularTolerance = UniMath.AngularEpsilon)
        {
            if (float.IsNaN(positionTolerance) || float.IsInfinity(positionTolerance) || positionTolerance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(positionTolerance), "The tolerance must be finite and nonnegative.");

            if (float.IsNaN(angularTolerance) || float.IsInfinity(angularTolerance) || angularTolerance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(angularTolerance), "The tolerance must be finite and nonnegative.");

            if (float.IsNaN(Rotation) || float.IsInfinity(Rotation)
                || float.IsNaN(other.Rotation) || float.IsInfinity(other.Rotation))
                throw new ArgumentException("Transform rotations must be finite.");

            var samePosition = UniMath.Is(Position, other.Position, positionTolerance);
            var angle = System.Math.IEEERemainder((double)Rotation - other.Rotation, 2d * System.Math.PI);
            return samePosition && System.Math.Abs(angle) <= angularTolerance;
        }

        public readonly bool Equals(Transform2D other) =>
            Position.Equals(other.Position)
            && Rotation.Equals(other.Rotation);

        public override readonly bool Equals(object obj) => obj is Transform2D other && Equals(other);

        public static bool operator ==(Transform2D left, Transform2D right) => left.Equals(right);
        public static bool operator !=(Transform2D left, Transform2D right) => !left.Equals(right);

        public override readonly int GetHashCode() => HashCode.Combine(Position, Rotation);

        public override readonly string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "Position: {0}, Rotation: {1}", Position, Rotation);
    }
}
