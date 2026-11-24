#if UNITY_2022_3_OR_NEWER
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class PolygonCollider2DUnity : Collider2D
    {
        public U.PolygonCollider2D Source { get { ThrowIfInvalid(); return (U.PolygonCollider2D)_reference.Value; } }
        private readonly UnityObjectReference _reference;


        internal PolygonCollider2DUnity(UnitySpaceContext2D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference)) => _reference = reference;
    }
}
#endif
