using System.Collections.Generic;
using System.Numerics;

namespace BlackThunder.GeoSystem.Geometry.Planar.Shapers
{
    public class Bezier2 : Shaper
    {
        protected override int RequiredHandleCount => 1;

        protected override bool TryCreatePositions(
            Vector2 from,
            Vector2 to,
            IReadOnlyList<Vector2> handles,
            float targetLength,
            List<Vector2> positions) =>
            BezierSampling.TryCreate(new[] { from, handles[0], to }, targetLength, positions);
    }
}
