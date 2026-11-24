using System.Numerics;

namespace BlackThunder.UniEngine.Mathematics
{
    public interface IVectorFunction
    {
        Vector2 Evaluate(float t);
    }
}
