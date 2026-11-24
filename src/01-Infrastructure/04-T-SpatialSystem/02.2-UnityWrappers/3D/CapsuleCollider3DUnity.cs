#if UNITY_2022_3_OR_NEWER
using N = System.Numerics;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class CapsuleCollider3DUnity : CapsuleCollider3D
    {
        public U.CapsuleCollider Source { get { ThrowIfInvalid(); return (U.CapsuleCollider)_reference.Value; } }
        private readonly UnityObjectReference _reference;

        private static U.CapsuleCollider Native(UnityObjectReference reference) => (U.CapsuleCollider)reference.Value;


        internal CapsuleCollider3DUnity(UnitySpaceContext3D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference),
                UnityBindingUtility.Property<float>(
                    () => Native(reference).radius,
                    value => Native(reference).radius = value),
                UnityBindingUtility.Property<float>(
                    () => Native(reference).height,
                    value => Native(reference).height = value),
                UnityBindingUtility.Property<N.Vector3>(
                    () => UnityBindingUtility.Common(Native(reference).center),
                    value => Native(reference).center = UnityBindingUtility.Native(value)),
                UnityBindingUtility.Property<CapsuleDirection3D>(
                    () => (CapsuleDirection3D)Native(reference).direction,
                    value => Native(reference).direction = (int)value)) =>
                    _reference = reference;
    }
}
#endif
