using System;
using System.Numerics;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.Samples
{
    public static class ObjectsSnippet
    {
        // Content
        public static void CompareIntegerVectors()
        {
            var cell = new Vector2Int(2, 3);
            var same = cell == new Vector2Int(2, 3); // true, exact comparison
            var near = cell.Is(new Vector2Int(3, 4), 1.5f); // true, the distance 1.41 is within 1.5
            var origin = Vector3Int.Zero; // (0, 0, 0)
        }

        public static void CompareTransforms()
        {
            // Is compares poses within tolerances; == compares values exactly.
            var pose = new Transform2D(new Vector2(1f, 2f), MathF.PI / 2f);
            var turned = new Transform2D(new Vector2(1f, 2f), MathF.PI / 2f + 2f * MathF.PI);
            var samePose = pose.Is(turned); // true, a full turn is the same rotation
            var equalPose = pose == turned; // false

            var spawn = new Transform3D(Vector3.UnitX, Quaternion.Identity);
            var flipped = new Transform3D(Vector3.UnitX, Quaternion.Negate(Quaternion.Identity));
            var sameSpawn = spawn.Is(flipped); // true, q and -q are the same rotation
            var equalSpawn = spawn == flipped; // false
        }
    }
}
