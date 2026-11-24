#if UNITY_2022_3_OR_NEWER
using N = System.Numerics;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class SphereCollider3DUnity : SphereCollider3D
    {
        public U.SphereCollider Source { get { ThrowIfInvalid(); return (U.SphereCollider)_reference.Value; } }
        private readonly UnityObjectReference _reference;

        private static U.SphereCollider Native(UnityObjectReference reference) => (U.SphereCollider)reference.Value;


        internal SphereCollider3DUnity(UnitySpaceContext3D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference),
                UnityBindingUtility.Property<float>(
                    () => Native(reference).radius,
                    value => Native(reference).radius = value),
                UnityBindingUtility.Property<N.Vector3>(
                    () => UnityBindingUtility.Common(Native(reference).center),
                    value => Native(reference).center = UnityBindingUtility.Native(value))) =>
                    _reference = reference;
    }
}
#endif
