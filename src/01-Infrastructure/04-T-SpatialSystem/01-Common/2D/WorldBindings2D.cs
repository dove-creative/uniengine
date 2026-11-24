using System;
using System.Numerics;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class CollisionWorldBindings2D
    {
        public Action<float> Update { get; }
        public Func<int, int, bool> GetLayerCollision { get; }
        public Action<int, int, bool> SetLayerCollision { get; }
        public Func<Transform2D, Vector2, BoxCollider2D> CreateBox { get; }
        public Func<Transform2D, float, CircleCollider2D> CreateCircle { get; }
        public Func<Transform2D, Vector2, CapsuleDirection2D, CapsuleCollider2D> CreateCapsule { get; }


        public CollisionWorldBindings2D(
            Action<float> update, Func<int, int, bool> getLayerCollision, Action<int, int, bool> setLayerCollision,
            Func<Transform2D, Vector2, BoxCollider2D> createBox,
            Func<Transform2D, float, CircleCollider2D> createCircle,
            Func<Transform2D, Vector2, CapsuleDirection2D, CapsuleCollider2D> createCapsule)
        {
            Update = SpatialValidation.Required(update, nameof(update));
            GetLayerCollision = SpatialValidation.Required(getLayerCollision, nameof(getLayerCollision));
            SetLayerCollision = SpatialValidation.Required(setLayerCollision, nameof(setLayerCollision));
            CreateBox = SpatialValidation.Required(createBox, nameof(createBox));
            CreateCircle = SpatialValidation.Required(createCircle, nameof(createCircle));
            CreateCapsule = SpatialValidation.Required(createCapsule, nameof(createCapsule));
        }
    }

}
