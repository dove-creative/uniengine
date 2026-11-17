using System.Collections.Generic;
using System.Numerics;

namespace BlackThunder.GeoSystem.Geometry.Planar.Shapers
{
    public class Bezier3 : Shaper
    {
        protected override int RequiredHandleCount => 2;

        protected override bool TryCreatePositions(
            Vector2 from,
            Vector2 to,
            IReadOnlyList<Vector2> handles,
            float targetLength,
            List<Vector2> positions) =>
            BezierSampling.TryCreate(new[] { from, handles[0], handles[1], to }, targetLength, positions);
    }
}
