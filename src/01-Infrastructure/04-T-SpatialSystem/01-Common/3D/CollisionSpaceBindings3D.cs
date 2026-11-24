using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class CollisionSpaceBindings3D
    {
        public CollisionWorldBindings3D World { get; }
        public Func<bool> IsValid { get; }
        public Func<Vector3, Vector3, float, SpatialQueryFilter, IReadOnlyList<SpatialRaycastHit3D>> RaycastAll { get; }
        public Func<Transform3D, Vector3, SpatialQueryFilter, IReadOnlyList<Collider3D>> OverlapBox { get; }
        public Func<Vector3, float, SpatialQueryFilter, IReadOnlyList<Collider3D>> OverlapSphere { get; }
        public Func<Vector3, Vector3, float, SpatialQueryFilter, IReadOnlyList<Collider3D>> OverlapCapsule { get; }
        public Func<Action<CollisionNotification3D>, Action> Subscribe { get; }
        public Action Release { get; }


        public CollisionSpaceBindings3D(
            Func<bool> isValid,
            Func<Vector3, Vector3, float, SpatialQueryFilter, IReadOnlyList<SpatialRaycastHit3D>> raycastAll,
            Func<Transform3D, Vector3, SpatialQueryFilter, IReadOnlyList<Collider3D>> overlapBox,
            Func<Vector3, float, SpatialQueryFilter, IReadOnlyList<Collider3D>> overlapSphere,
            Func<Vector3, Vector3, float, SpatialQueryFilter, IReadOnlyList<Collider3D>> overlapCapsule,
            Func<Action<CollisionNotification3D>, Action> subscribe,
            CollisionWorldBindings3D world,
            Action release = null)
        {
            World = SpatialValidation.Required(world, nameof(world));
            IsValid = SpatialValidation.Required(isValid, nameof(isValid));
            RaycastAll = SpatialValidation.Required(raycastAll, nameof(raycastAll));
            OverlapBox = SpatialValidation.Required(overlapBox, nameof(overlapBox));
            OverlapSphere = SpatialValidation.Required(overlapSphere, nameof(overlapSphere));
            OverlapCapsule = SpatialValidation.Required(overlapCapsule, nameof(overlapCapsule));
            Subscribe = SpatialValidation.Required(subscribe, nameof(subscribe));
            Release = release;
        }
    }
}
