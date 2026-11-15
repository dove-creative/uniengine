using System;
using System.Globalization;
using System.Numerics;
using BlackThunder.UniEngine.Mathematics;

namespace BlackThunder.UniEngine.Objects
{
    public struct Transform3D : IEquatable<Transform3D>
    {
        // Front
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }

        public static Transform3D Identity => new(Vector3.Zero, Quaternion.Identity);


        // Content
        public Transform3D(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public readonly bool Is(
            Transform3D other,
            float positionTolerance = UniMath.Epsilon,
            float angularTolerance = UniMath.AngularEpsilon)
        {
            if (float.IsNaN(positionTolerance) || float.IsInfinity(positionTolerance) || positionTolerance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(positionTolerance), "The tolerance must be finite and nonnegative.");

            if (float.IsNaN(angularTolerance) || float.IsInfinity(angularTolerance) || angularTolerance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(angularTolerance), "The tolerance must be finite and nonnegative.");

            var samePosition = UniMath.Is(Position, other.Position, positionTolerance);
            var sameRotation = UniMath.IsSameRotationAs(Rotation, other.Rotation, angularTolerance);
            return samePosition && sameRotation;
        }

        public readonly bool Equals(Transform3D other) =>
            Position.Equals(other.Position)
            && Rotation.Equals(other.Rotation);

        public override readonly bool Equals(object obj) => obj is Transform3D other && Equals(other);

        public static bool operator ==(Transform3D left, Transform3D right) => left.Equals(right);
        public static bool operator !=(Transform3D left, Transform3D right) => !left.Equals(right);

        public override readonly int GetHashCode() => HashCode.Combine(Position, Rotation);

        public override readonly string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "Position: {0}, Rotation: {1}", Position, Rotation);
    }
}
