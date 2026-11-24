#if UNITY_2022_3_OR_NEWER
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class MeshCollider3DUnity : Collider3D
    {
        public U.MeshCollider Source { get { ThrowIfInvalid(); return (U.MeshCollider)_reference.Value; } }
        private readonly UnityObjectReference _reference;


        internal MeshCollider3DUnity(UnitySpaceContext3D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference)) => _reference = reference;
    }
}
#endif
