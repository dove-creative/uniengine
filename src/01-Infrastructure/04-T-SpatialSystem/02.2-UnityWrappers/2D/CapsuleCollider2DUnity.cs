#if UNITY_2022_3_OR_NEWER
using N = System.Numerics;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class CapsuleCollider2DUnity : CapsuleCollider2D
    {
        public U.CapsuleCollider2D Source { get { ThrowIfInvalid(); return (U.CapsuleCollider2D)_reference.Value; } }
        private readonly UnityObjectReference _reference;

        private static U.CapsuleCollider2D Native(UnityObjectReference reference) =>
            (U.CapsuleCollider2D)reference.Value;


        internal CapsuleCollider2DUnity(UnitySpaceContext2D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference),
                UnityBindingUtility.Property<N.Vector2>(
                    () => UnityBindingUtility.Common(Native(reference).size),
                    value => Native(reference).size = UnityBindingUtility.Native(value)),
                UnityBindingUtility.Property<N.Vector2>(
                    () => UnityBindingUtility.Common(Native(reference).offset),
                    value => Native(reference).offset = UnityBindingUtility.Native(value)),
                UnityBindingUtility.Property<CapsuleDirection2D>(
                    () => UnityBindingUtility.Common(Native(reference).direction),
                    value => Native(reference).direction = UnityBindingUtility.Native(value))) =>
                    _reference = reference;
    }
}
#endif
