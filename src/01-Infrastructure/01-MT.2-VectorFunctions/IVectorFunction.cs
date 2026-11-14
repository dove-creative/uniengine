using System.Numerics;

namespace Blackthunder.UniEngine.Mathematics
{
    public interface IVectorFunction
    {
        Vector2 Evaluate(float t);
    }
}
