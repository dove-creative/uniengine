#if UNITY_2022_3_OR_NEWER
using N = System.Numerics;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class BoxCollider2DUnity : BoxCollider2D
    {
        public U.BoxCollider2D Source { get { ThrowIfInvalid(); return (U.BoxCollider2D)_reference.Value; } }
        private readonly UnityObjectReference _reference;

        private static U.BoxCollider2D Native(UnityObjectReference reference) => (U.BoxCollider2D)reference.Value;


        internal BoxCollider2DUnity(UnitySpaceContext2D context, UnityObjectReference reference)
            : base(context.Space, context.ColliderBindings(reference),
                UnityBindingUtility.Property<N.Vector2>(
                    () => UnityBindingUtility.Common(Native(reference).size),
                    value => Native(reference).size = UnityBindingUtility.Native(value)),
                UnityBindingUtility.Property<N.Vector2>(
                    () => UnityBindingUtility.Common(Native(reference).offset),
                    value => Native(reference).offset = UnityBindingUtility.Native(value))) =>
                    _reference = reference;
    }
}
#endif
