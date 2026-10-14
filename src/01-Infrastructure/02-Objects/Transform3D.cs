using System;
using System.Globalization;
using System.Numerics;

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

        #region Equals
        public readonly bool Equals(Transform3D other) =>
            Position.Equals(other.Position)
            && Rotation.Equals(other.Rotation);

        public override readonly bool Equals(object obj) =>
            obj is Transform3D other
            && Equals(other);

        public static bool operator ==(Transform3D left, Transform3D right) => left.Equals(right);
        public static bool operator !=(Transform3D left, Transform3D right) => !left.Equals(right);

        public override readonly int GetHashCode() => HashCode.Combine(Position, Rotation);
        #endregion


        public override readonly string ToString() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "Position: {0}, Rotation: {1}",
                Position,
                Rotation);
    }
}
