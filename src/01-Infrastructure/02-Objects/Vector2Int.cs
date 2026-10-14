using System;
using System.Globalization;

namespace BlackThunder.UniEngine.Objects
{
    public struct Vector2Int : IEquatable<Vector2Int>
    {
        // Front
        public int X { get; set; }
        public int Y { get; set; }

        public static Vector2Int Zero => new(0, 0);


        // Content
        public Vector2Int(int x, int y)
        {
            X = x;
            Y = y;
        }

        #region Equals
        public readonly bool Equals(Vector2Int other) =>
            X == other.X
            && Y == other.Y;

        public override readonly bool Equals(object obj) =>
            obj is Vector2Int other
            && Equals(other);

        public static bool operator ==(Vector2Int left, Vector2Int right) => left.Equals(right);
        public static bool operator !=(Vector2Int left, Vector2Int right) => !left.Equals(right);

        public override readonly int GetHashCode() => HashCode.Combine(X, Y);
        #endregion


        public override readonly string ToString() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "<{0}, {1}>",
                X,
                Y);
    }
}
