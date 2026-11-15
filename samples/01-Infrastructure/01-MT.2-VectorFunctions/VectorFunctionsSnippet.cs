using System;
using System.Numerics;
using BlackThunder.UniEngine.Mathematics;

namespace BlackThunder.UniEngine.Samples
{
    public static class VectorFunctionsSnippet
    {
        // Content
        public static void Evaluate()
        {
            // Both functions map a parameter t to a position through the same contract.
            IVectorFunction line = new AffineLine(new Vector2(1f, 2f), new Vector2(4f, 6f));
            var start = line.Evaluate(0f); // (1, 2)
            var midpoint = line.Evaluate(0.5f); // (3, 5)

            IVectorFunction curve = new QuadraticBezier(Vector2.Zero, new Vector2(2f, 4f), new Vector2(4f, 0f));
            var onCurve = curve.Evaluate(0.5f); // (2, 2)
            var extrapolated = curve.Evaluate(2f); // (8, -16), t outside 0..1 is not clamped
        }

        public static void Normalize()
        {
            // A normalized line moves one unit per t.
            var unitLine = new AffineLine(Vector2.Zero, new Vector2(3f, 4f), normalize: true);
            var direction = unitLine.DirectionVector; // (0.6, 0.8)
            var position = unitLine.Evaluate(5f); // (3, 4)

            try
            {
                new AffineLine(Vector2.One, Vector2.Zero).Normalize();
            }
            catch (InvalidOperationException)
            {
                // A zero or non-finite direction cannot be normalized.
            }
        }
    }
}
