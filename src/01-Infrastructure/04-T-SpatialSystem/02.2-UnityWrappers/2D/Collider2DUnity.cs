#if UNITY_2022_3_OR_NEWER
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class Collider2DUnity : Collider2D
    {
        public U.Collider2D Source { get { ThrowIfInvalid(); return (U.Collider2D)_reference.Value; } }
        private readonly UnityObjectReference _reference;


        internal Collider2DUnity(UnitySpaceContext2D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference)) => _reference = reference;
    }
}
#endif
