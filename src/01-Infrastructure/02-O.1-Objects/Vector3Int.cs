using System;
using System.Globalization;

namespace BlackThunder.UniEngine.Objects
{
    public struct Vector3Int : IEquatable<Vector3Int>
    {
        // Front
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public static Vector3Int Zero => new(0, 0, 0);


        // Content
        public Vector3Int(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly bool Equals(Vector3Int other) => X == other.X && Y == other.Y && Z == other.Z;

        public override readonly bool Equals(object obj) => obj is Vector3Int other && Equals(other);

        public static bool operator ==(Vector3Int left, Vector3Int right) => left.Equals(right);
        public static bool operator !=(Vector3Int left, Vector3Int right) => !left.Equals(right);

        public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override readonly string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "<{0}, {1}, {2}>", X, Y, Z);
    }
}
