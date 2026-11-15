using System.Numerics;
using BlackThunder.UniEngine.Mathematics;

namespace BlackThunder.UniEngine.Samples
{
    public static class MathSnippet
    {
        // Content
        public static void CompareAndSnap()
        {
            var close = 1f.Is(1.125f, 0.125f); // true, including the boundary
            var zero = Vector4.Zero.IsZero(); // true
            var grid = new Vector2(2.5f, -3.5f).Snap(1f); // (2, -4)
            var snapped = 1.125f.SnapTo(1f, 0.125f, out var isSnapped); // 1, isSnapped = true
        }

        public static void CompareDirectionsAndRotations()
        {
            var parallel = Vector2.UnitX.IsParallelWith(-Vector2.UnitX, out var sameDirection);
            // parallel = true, sameDirection = false

            var identity = Quaternion.Identity;
            var sameRotation = identity.IsSameRotationAs(Quaternion.Negate(identity)); // true
            var halfTurn = identity.IsHalfTurnFrom(new Quaternion(0f, 1f, 0f, 0f)); // true

            // The angular tolerance is in radians.
            var rotated = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.01f);
            var withinAngle = identity.IsSameRotationAs(rotated, 0.02f); // true
        }
    }
}
