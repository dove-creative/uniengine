using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class CollisionSpaceBindings2D
    {
        public CollisionWorldBindings2D World { get; }
        public Func<bool> IsValid { get; }
        public Func<Vector2, Vector2, float, SpatialQueryFilter, IReadOnlyList<SpatialRaycastHit2D>> RaycastAll { get; }
        public Func<Transform2D, Vector2, SpatialQueryFilter, IReadOnlyList<Collider2D>> OverlapBox { get; }
        public Func<Vector2, float, SpatialQueryFilter, IReadOnlyList<Collider2D>> OverlapCircle { get; }
        public Func<
            Transform2D,
            Vector2,
            CapsuleDirection2D,
            SpatialQueryFilter,
            IReadOnlyList<Collider2D>> OverlapCapsule { get; }
        public Func<Action<CollisionNotification2D>, Action> Subscribe { get; }
        public Action Release { get; }


        public CollisionSpaceBindings2D(
            Func<bool> isValid,
            Func<Vector2, Vector2, float, SpatialQueryFilter, IReadOnlyList<SpatialRaycastHit2D>> raycastAll,
            Func<Transform2D, Vector2, SpatialQueryFilter, IReadOnlyList<Collider2D>> overlapBox,
            Func<Vector2, float, SpatialQueryFilter, IReadOnlyList<Collider2D>> overlapCircle,
            Func<
                Transform2D,
                Vector2,
                CapsuleDirection2D,
                SpatialQueryFilter,
                IReadOnlyList<Collider2D>> overlapCapsule,
            Func<Action<CollisionNotification2D>, Action> subscribe,
            CollisionWorldBindings2D world,
            Action release = null)
        {
            World = SpatialValidation.Required(world, nameof(world));
            IsValid = SpatialValidation.Required(isValid, nameof(isValid));
            RaycastAll = SpatialValidation.Required(raycastAll, nameof(raycastAll));
            OverlapBox = SpatialValidation.Required(overlapBox, nameof(overlapBox));
            OverlapCircle = SpatialValidation.Required(overlapCircle, nameof(overlapCircle));
            OverlapCapsule = SpatialValidation.Required(overlapCapsule, nameof(overlapCapsule));
            Subscribe = SpatialValidation.Required(subscribe, nameof(subscribe));
            Release = release;
        }
    }
}
