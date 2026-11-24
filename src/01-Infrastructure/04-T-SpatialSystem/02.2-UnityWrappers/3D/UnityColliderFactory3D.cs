#if UNITY_2022_3_OR_NEWER
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    internal static class UnityColliderFactory3D
    {
        public static Collider3D Create(UnitySpaceContext3D context, UnityObjectReference reference) =>
            reference.Value switch
            {
                U.BoxCollider _ => new BoxCollider3DUnity(context, reference),
                U.SphereCollider _ => new SphereCollider3DUnity(context, reference),
                U.CapsuleCollider _ => new CapsuleCollider3DUnity(context, reference),
                U.MeshCollider _ => new MeshCollider3DUnity(context, reference),
                U.TerrainCollider _ => new TerrainCollider3DUnity(context, reference),
                _ => new Collider3DUnity(context, reference),
            };
    }
}
#endif
