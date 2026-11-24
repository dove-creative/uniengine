#if UNITY_2022_3_OR_NEWER
using N = System.Numerics;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class CircleCollider2DUnity : CircleCollider2D
    {
        public U.CircleCollider2D Source { get { ThrowIfInvalid(); return (U.CircleCollider2D)_reference.Value; } }
        private readonly UnityObjectReference _reference;

        private static U.CircleCollider2D Native(UnityObjectReference reference) => (U.CircleCollider2D)reference.Value;


        internal CircleCollider2DUnity(UnitySpaceContext2D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference),
                UnityBindingUtility.Property<float>(
                    () => Native(reference).radius,
                    value => Native(reference).radius = value),
                UnityBindingUtility.Property<N.Vector2>(
                    () => UnityBindingUtility.Common(Native(reference).offset),
                    value => Native(reference).offset = UnityBindingUtility.Native(value))) =>
                    _reference = reference;
    }
}
#endif
