#if UNITY_2022_3_OR_NEWER
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class EdgeCollider2DUnity : Collider2D
    {
        public U.EdgeCollider2D Source { get { ThrowIfInvalid(); return (U.EdgeCollider2D)_reference.Value; } }
        private readonly UnityObjectReference _reference;


        internal EdgeCollider2DUnity(UnitySpaceContext2D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference)) => _reference = reference;
    }
}
#endif
