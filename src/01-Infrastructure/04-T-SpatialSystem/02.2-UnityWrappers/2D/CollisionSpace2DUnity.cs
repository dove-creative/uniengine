#if UNITY_2022_3_OR_NEWER
using BlackThunder.UniEngine.Objects;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    public class CollisionSpace2DUnity : CollisionSpace2D
    {
        private readonly UnitySpaceContext2D _context;


        public CollisionSpace2DUnity() : this(new UnitySpaceContext2D()) { }

        private CollisionSpace2DUnity(UnitySpaceContext2D context) : base(context.CreateBindings())
        {
            _context = context;
            context.Attach(this);
        }

        public Collider2D CreateUnityCollider<T>(Transform2D? pose = null)
            where T : U.Collider2D
        {
            ThrowIfInvalid();
            return _context.CreateUnityCollider<T>(CreationPose(pose));
        }
    }
}
#endif
