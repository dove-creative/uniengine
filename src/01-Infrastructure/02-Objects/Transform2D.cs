using System;
using System.Globalization;
using System.Numerics;

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

        #region Equals
        public readonly bool Equals(Transform2D other) =>
            Position.Equals(other.Position)
            && Rotation.Equals(other.Rotation);

        public override readonly bool Equals(object obj) =>
            obj is Transform2D other
            && Equals(other);

        public static bool operator ==(Transform2D left, Transform2D right) => left.Equals(right);
        public static bool operator !=(Transform2D left, Transform2D right) => !left.Equals(right);

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
