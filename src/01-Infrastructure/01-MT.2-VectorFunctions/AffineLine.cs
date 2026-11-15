using System;
using System.Numerics;

namespace BlackThunder.UniEngine.Mathematics
{
    public class AffineLine : IVectorFunction
    {
        // Front
        public Vector2 Origin { get; set; }
        public Vector2 DirectionVector { get; set; }


        // Content
        public AffineLine() { }
        public AffineLine(Vector2 origin, Vector2 directionVector, bool normalize = false)
        {
            Origin = origin;
            DirectionVector = directionVector;
            if (normalize) Normalize();
        }

        public void Normalize()
        {
            var direction = DirectionVector;
            var length = System.Math.Sqrt((double)direction.X * direction.X + (double)direction.Y * direction.Y);
            if (!(length > 0d) || double.IsInfinity(length))
                throw new InvalidOperationException("A direction must be finite and nonzero to be normalized.");

            DirectionVector = new Vector2((float)(direction.X / length), (float)(direction.Y / length));
        }

        public Vector2 Evaluate(float t) => Origin + DirectionVector * t;
    }
}
