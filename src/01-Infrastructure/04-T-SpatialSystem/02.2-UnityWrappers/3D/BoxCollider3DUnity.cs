#if UNITY_2022_3_OR_NEWER
using N = System.Numerics;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class BoxCollider3DUnity : BoxCollider3D
    {
        public U.BoxCollider Source { get { ThrowIfInvalid(); return (U.BoxCollider)_reference.Value; } }
        private readonly UnityObjectReference _reference;

        private static U.BoxCollider Native(UnityObjectReference reference) => (U.BoxCollider)reference.Value;


        internal BoxCollider3DUnity(UnitySpaceContext3D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference),
                UnityBindingUtility.Property<N.Vector3>(
                    () => UnityBindingUtility.Common(Native(reference).size),
                    value => Native(reference).size = UnityBindingUtility.Native(value)),
                UnityBindingUtility.Property<N.Vector3>(
                    () => UnityBindingUtility.Common(Native(reference).center),
                    value => Native(reference).center = UnityBindingUtility.Native(value))) =>
                    _reference = reference;
    }
}
#endif
