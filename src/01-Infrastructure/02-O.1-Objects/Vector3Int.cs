using System;
using System.Globalization;
using Blackthunder.UniEngine.Mathematics;

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

        public readonly bool Is(Vector3Int other, float tolerance = UniMath.Epsilon)
        {
            if (float.IsNaN(tolerance) || float.IsInfinity(tolerance) || tolerance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(tolerance), "The tolerance must be finite and nonnegative.");

            var x = (double)X - other.X;
            var y = (double)Y - other.Y;
            var z = (double)Z - other.Z;
            return x * x + y * y + z * z <= (double)tolerance * tolerance;
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
