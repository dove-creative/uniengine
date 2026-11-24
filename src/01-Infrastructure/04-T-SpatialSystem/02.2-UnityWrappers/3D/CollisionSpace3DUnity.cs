#if UNITY_2022_3_OR_NEWER
using BlackThunder.UniEngine.Objects;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class CollisionSpace3DUnity : CollisionSpace3D
    {
        private readonly UnitySpaceContext3D _context;


        public CollisionSpace3DUnity() : this(new UnitySpaceContext3D()) { }

        private CollisionSpace3DUnity(UnitySpaceContext3D context) : base(context.CreateBindings())
        {
            _context = context;
            context.Attach(this);
        }

        public Collider3D CreateUnityCollider<T>(Transform3D? pose = null)
            where T : U.Collider
        {
            ThrowIfInvalid();
            return _context.CreateUnityCollider<T>(CreationPose(pose));
        }
    }
}
#endif
