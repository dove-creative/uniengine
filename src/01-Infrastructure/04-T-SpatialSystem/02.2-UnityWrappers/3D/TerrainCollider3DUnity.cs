#if UNITY_2022_3_OR_NEWER
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class TerrainCollider3DUnity : Collider3D
    {
        public U.TerrainCollider Source { get { ThrowIfInvalid(); return (U.TerrainCollider)_reference.Value; } }
        private readonly UnityObjectReference _reference;


        internal TerrainCollider3DUnity(UnitySpaceContext3D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference)) => _reference = reference;
    }
}
#endif
