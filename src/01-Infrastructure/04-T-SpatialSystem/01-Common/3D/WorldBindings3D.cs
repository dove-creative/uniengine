using System;
using System.Numerics;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class CollisionWorldBindings3D
    {
        public Action<float> Update { get; }
        public Func<int, int, bool> GetLayerCollision { get; }
        public Action<int, int, bool> SetLayerCollision { get; }
        public Func<Transform3D, Vector3, BoxCollider3D> CreateBox { get; }
        public Func<Transform3D, float, SphereCollider3D> CreateSphere { get; }
        public Func<Transform3D, float, float, CapsuleDirection3D, CapsuleCollider3D> CreateCapsule { get; }


        public CollisionWorldBindings3D(
            Action<float> update, Func<int, int, bool> getLayerCollision, Action<int, int, bool> setLayerCollision,
            Func<Transform3D, Vector3, BoxCollider3D> createBox,
            Func<Transform3D, float, SphereCollider3D> createSphere,
            Func<Transform3D, float, float, CapsuleDirection3D, CapsuleCollider3D> createCapsule)
        {
            Update = SpatialValidation.Required(update, nameof(update));
            GetLayerCollision = SpatialValidation.Required(getLayerCollision, nameof(getLayerCollision));
            SetLayerCollision = SpatialValidation.Required(setLayerCollision, nameof(setLayerCollision));
            CreateBox = SpatialValidation.Required(createBox, nameof(createBox));
            CreateSphere = SpatialValidation.Required(createSphere, nameof(createSphere));
            CreateCapsule = SpatialValidation.Required(createCapsule, nameof(createCapsule));
        }
    }

}
