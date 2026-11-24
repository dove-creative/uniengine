using System;
using System.Numerics;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class ColliderBindings3D
    {
        public SpatialProperty<Transform3D> Pose { get; }
        public SpatialProperty<bool> Enabled { get; }
        public SpatialProperty<bool> IsTrigger { get; }
        public SpatialProperty<int> Layer { get; }
        public Func<bool> IsValid { get; }
        public Action Release { get; }
        public Func<bool> IsActive { get; }
        public Action Prepare { get; }


        public ColliderBindings3D(
            SpatialProperty<Transform3D> pose,
            SpatialProperty<bool> enabled,
            SpatialProperty<bool> isTrigger,
            SpatialProperty<int> layer,
            Func<bool> isValid,
            Action release = null,
            Func<bool> isActive = null,
            Action prepare = null)
        {
            Pose = SpatialValidation.Required(pose, nameof(pose));
            Enabled = SpatialValidation.Required(enabled, nameof(enabled));
            IsTrigger = SpatialValidation.Required(isTrigger, nameof(isTrigger));
            Layer = SpatialValidation.Required(layer, nameof(layer));
            IsValid = SpatialValidation.Required(isValid, nameof(isValid));
            Release = release;
            IsActive = isActive;
            Prepare = prepare;
        }
    }

    public readonly struct SpatialRaycastHit3D
    {
        public Collider3D Collider { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public float Distance { get; }


        public SpatialRaycastHit3D(Collider3D collider, Vector3 point, Vector3 normal, float distance)
        {
            Collider = collider ?? throw new ArgumentNullException(nameof(collider));
            SpatialValidation.Vector(point, nameof(point));
            SpatialValidation.Vector(normal, nameof(normal));
            SpatialValidation.Nonnegative(distance, nameof(distance));
            Point = point;
            Normal = normal;
            Distance = distance;
        }
    }

    public readonly struct CollisionPair3D
    {
        public Collider3D First { get; }
        public Collider3D Second { get; }


        public CollisionPair3D(Collider3D first, Collider3D second)
        {
            First = first ?? throw new ArgumentNullException(nameof(first));
            Second = second ?? throw new ArgumentNullException(nameof(second));

            if (ReferenceEquals(first, second)) throw new ArgumentException("A pair must contain two colliders.");
        }

        public Collider3D Other(Collider3D collider)
        {
            if (ReferenceEquals(collider, First)) return Second;
            if (ReferenceEquals(collider, Second)) return First;

            throw new ArgumentException("The collider is not part of this pair.", nameof(collider));
        }
    }

    public readonly struct CollisionNotification3D
    {
        public CollisionPair3D Pair { get; }
        public bool IsTrigger { get; }
        public bool IsEntering { get; }
        public long RelationshipId { get; }

        // False once an applied setting has ended the relation, even if the setting has been restored since.
        internal bool Holds
        {
            get
            {
                var first = Pair.First;
                var second = Pair.Second;
                if (!first.Active || !second.Active) return false;
                if (first.ActiveRevision != _firstActiveRevision) return false;
                if (second.ActiveRevision != _secondActiveRevision) return false;

                var firstChanged = first.TriggerRevision != _firstTriggerRevision;
                var secondChanged = second.TriggerRevision != _secondTriggerRevision;
                var trigger = first.AppliedTrigger || second.AppliedTrigger;
                if (!IsTrigger) return !trigger && !firstChanged && !secondChanged;

                // With one change, only the unchanged collider can have kept the pair a trigger throughout.
                if (firstChanged != secondChanged) return (firstChanged ? second : first).AppliedTrigger;

                return trigger;
            }
        }
        private readonly long _firstActiveRevision;
        private readonly long _secondActiveRevision;
        private readonly long _firstTriggerRevision;
        private readonly long _secondTriggerRevision;


        // A backend gives each new relationship its own ID, reuses that ID for the exit,
        // and never announces the start of a relationship that has already ended.
        public CollisionNotification3D(
            CollisionPair3D pair, bool isTrigger, bool isEntering, long relationshipId)
        {
            if (pair.First == null || pair.Second == null)
                throw new ArgumentException("A pair is required.", nameof(pair));
            if (relationshipId <= 0) throw new ArgumentOutOfRangeException(nameof(relationshipId));

            Pair = pair;
            IsTrigger = isTrigger;
            IsEntering = isEntering;
            RelationshipId = relationshipId;
            _firstActiveRevision = pair.First.ActiveRevision;
            _secondActiveRevision = pair.Second.ActiveRevision;
            _firstTriggerRevision = pair.First.TriggerRevision;
            _secondTriggerRevision = pair.Second.TriggerRevision;
        }
    }
}
