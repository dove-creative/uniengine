#if UNITY_2022_3_OR_NEWER
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    internal static class UnityColliderFactory2D
    {
        public static Collider2D Create(UnitySpaceContext2D context, UnityObjectReference reference) =>
            reference.Value switch
            {
                U.BoxCollider2D _ => new BoxCollider2DUnity(context, reference),
                U.CircleCollider2D _ => new CircleCollider2DUnity(context, reference),
                U.CapsuleCollider2D _ => new CapsuleCollider2DUnity(context, reference),
                U.PolygonCollider2D _ => new PolygonCollider2DUnity(context, reference),
                U.EdgeCollider2D _ => new EdgeCollider2DUnity(context, reference),
                _ => new Collider2DUnity(context, reference),
            };
    }
}
#endif
