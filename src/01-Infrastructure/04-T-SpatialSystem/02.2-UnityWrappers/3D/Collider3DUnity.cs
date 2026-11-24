#if UNITY_2022_3_OR_NEWER
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class Collider3DUnity : Collider3D
    {
        public U.Collider Source { get { ThrowIfInvalid(); return (U.Collider)_reference.Value; } }
        private readonly UnityObjectReference _reference;


        internal Collider3DUnity(UnitySpaceContext3D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference)) => _reference = reference;
    }
}
#endif
