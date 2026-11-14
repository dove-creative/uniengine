using System.Numerics;

namespace Blackthunder.UniEngine.Mathematics
{
    public class QuadraticBezier : IVectorFunction
    {
        // Front
        public Vector2 P0 { get; set; }
        public Vector2 P1 { get; set; }
        public Vector2 P2 { get; set; }


        // Content
        public QuadraticBezier() { }
        public QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
        }

        public Vector2 Evaluate(float t)
        {
            var u = 1f - t;
            return u * u * P0 + 2f * u * t * P1 + t * t * P2;
        }
    }
}
