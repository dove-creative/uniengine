using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public class CollisionSpace3D : SpatialSpace
    {
        // Control
        public event Action<CollisionPair3D> CollisionEnter
        {
            add { ThrowIfInvalid(); _collisionEnter += value; }
            remove => _collisionEnter -= value;
        }
        public event Action<CollisionPair3D> CollisionExit
        {
            add { ThrowIfInvalid(); _collisionExit += value; }
            remove => _collisionExit -= value;
        }
        public event Action<CollisionPair3D> TriggerEnter
        {
            add { ThrowIfInvalid(); _triggerEnter += value; }
            remove => _triggerEnter -= value;
        }
        public event Action<CollisionPair3D> TriggerExit
        {
            add { ThrowIfInvalid(); _triggerExit += value; }
            remove => _triggerExit -= value;
        }

        // Internal
        private CollisionSpaceBindings3D _bindings;
        private Action _unsubscribe;
        private bool _updating;
        private readonly Queue<CollisionNotification3D> _notifications = new();
        private Action<CollisionPair3D> _collisionEnter;
        private Action<CollisionPair3D> _collisionExit;
        private Action<CollisionPair3D> _triggerEnter;
        private Action<CollisionPair3D> _triggerExit;


        // Content
        public CollisionSpace3D(CollisionSpaceBindings3D bindings)
            : base(SpatialValidation.Required(bindings, nameof(bindings)).IsValid, bindings.Release)
        {
            _bindings = bindings;
            _unsubscribe = bindings.Subscribe(Receive)
                ?? throw new ArgumentException("Subscription must return a disconnect action.", nameof(bindings));
        }

        public void Update(float deltaTime)
        {
            using var scope = SpatialDispatch.Enter();
            ThrowIfInvalid();
            SpatialValidation.Positive(deltaTime, nameof(deltaTime));
            if (_updating) throw new InvalidOperationException("A world update cannot be nested.");

            _updating = true;
            try
            {
                var handles = SnapshotHandles();
                foreach (Collider3D collider in handles) collider.FreezeSettings();
                foreach (Collider3D collider in handles) collider.ApplySettings();
                foreach (Collider3D collider in handles) collider.Prepare();
                if (!IsValid) return;

                var exits = ReconcileRelations(handles);
                if (!IsValid) return;
                if (!TryAdvance(deltaTime)) return;

                foreach (var exit in exits)
                {
                    if (!IsValid) break;
                    Dispatch(exit);
                }
                while (IsValid && _notifications.Count != 0) Dispatch(_notifications.Dequeue());
            }
            finally { _updating = false; }
        }

        // A failed step blames neither an object nor the world, so it ends only this update.
        private bool TryAdvance(float deltaTime)
        {
            try
            {
                _bindings.World.Update(deltaTime);
                return true;
            }
            catch (SpatialEngineException) { throw; }
            catch (Exception exception) when (!SpatialDispatch.IsPropagating(exception))
            {
                SpatialDispatch.Report(exception);
                return false;
            }
        }

        public bool GetLayerCollision(int first, int second)
        {
            ThrowIfInvalid();
            SpatialValidation.Layer(first, nameof(first));
            SpatialValidation.Layer(second, nameof(second));
            return _bindings.World.GetLayerCollision(first, second);
        }

        public void SetLayerCollision(int first, int second, bool enabled)
        {
            ThrowIfInvalid();
            SpatialValidation.Layer(first, nameof(first));
            SpatialValidation.Layer(second, nameof(second));
            _bindings.World.SetLayerCollision(first, second, enabled);
        }

        public BoxCollider3D CreateBoxCollider(Vector3 size, Transform3D? pose = null)
        {
            var actualPose = CreationPose(pose);
            SpatialValidation.Size(size, nameof(size));
            return RequireCreated(_bindings.World.CreateBox(actualPose, size));
        }

        public SphereCollider3D CreateSphereCollider(float radius, Transform3D? pose = null)
        {
            var actualPose = CreationPose(pose);
            SpatialValidation.Nonnegative(radius, nameof(radius));
            return RequireCreated(_bindings.World.CreateSphere(actualPose, radius));
        }

        protected Transform3D CreationPose(Transform3D? pose)
        {
            ThrowIfInvalid();
            var actualPose = pose ?? new Transform3D(Vector3.Zero, Quaternion.Identity);
            SpatialValidation.Pose(actualPose, nameof(pose));
            return actualPose;
        }

        public CapsuleCollider3D CreateCapsuleCollider(float radius, float height,
            CapsuleDirection3D direction = CapsuleDirection3D.Y, Transform3D? pose = null)
        {
            var actualPose = CreationPose(pose);
            SpatialValidation.Nonnegative(radius, nameof(radius));
            SpatialValidation.Nonnegative(height, nameof(height));
            SpatialValidation.EnumValue(direction, nameof(direction));
            return RequireCreated(_bindings.World.CreateCapsule(actualPose, radius, height, direction));
        }

        public bool Raycast(
            Vector3 origin, Vector3 direction, out SpatialRaycastHit3D hit,
            float maxDistance = float.PositiveInfinity, SpatialQueryFilter? filter = null)
        {
            var hits = RaycastAll(origin, direction, maxDistance, filter);
            hit = hits.Count == 0 ? default : hits[0];
            return hits.Count != 0;
        }

        public IReadOnlyList<SpatialRaycastHit3D> RaycastAll(
            Vector3 origin, Vector3 direction,
            float maxDistance = float.PositiveInfinity, SpatialQueryFilter? filter = null)
        {
            ThrowIfInvalid();
            SpatialValidation.Vector(origin, nameof(origin));
            SpatialValidation.Vector(direction, nameof(direction));
            SpatialValidation.Distance(maxDistance, nameof(maxDistance));
            var normalized = SpatialValidation.Direction(direction, nameof(direction));

            var source = _bindings.RaycastAll(origin, normalized, maxDistance, filter ?? SpatialQueryFilter.Default)
                ?? throw new InvalidOperationException("The backend returned no raycast collection.");
            var hits = new List<SpatialRaycastHit3D>(source.Count);

            foreach (var hit in source)
            {
                if (hit.Collider != null && ReferenceEquals(hit.Collider.Space, this) && !hit.Collider.IsValid)
                    continue;
                RequireCollider(hit.Collider);
                hits.Add(hit);
            }

            hits.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            return hits.AsReadOnly();
        }

        public IReadOnlyList<Collider3D> OverlapBox(Transform3D pose, Vector3 size, SpatialQueryFilter? filter = null)
        {
            ThrowIfInvalid();
            SpatialValidation.Pose(pose, nameof(pose));
            SpatialValidation.Size(size, nameof(size));
            return CopyColliders(_bindings.OverlapBox(pose, size, filter ?? SpatialQueryFilter.Default));
        }

        public IReadOnlyList<Collider3D> OverlapSphere(Vector3 center, float radius, SpatialQueryFilter? filter = null)
        {
            ThrowIfInvalid();
            SpatialValidation.Vector(center, nameof(center));
            SpatialValidation.Nonnegative(radius, nameof(radius));
            return CopyColliders(_bindings.OverlapSphere(center, radius, filter ?? SpatialQueryFilter.Default));
        }

        public IReadOnlyList<Collider3D> OverlapCapsule(
            Vector3 firstCenter, Vector3 secondCenter, float radius, SpatialQueryFilter? filter = null)
        {
            ThrowIfInvalid();
            SpatialValidation.Vector(firstCenter, nameof(firstCenter));
            SpatialValidation.Vector(secondCenter, nameof(secondCenter));
            SpatialValidation.Nonnegative(radius, nameof(radius));
            return CopyColliders(_bindings.OverlapCapsule(
                firstCenter,
                secondCenter,
                radius,
                filter ?? SpatialQueryFilter.Default));
        }

        private IReadOnlyList<Collider3D> CopyColliders(IReadOnlyList<Collider3D> source)
        {
            if (source == null) throw new InvalidOperationException("The backend returned no overlap collection.");

            var seen = new HashSet<Collider3D>();
            var colliders = new List<Collider3D>();

            foreach (var collider in source)
            {
                if (collider != null && ReferenceEquals(collider.Space, this) && !collider.IsValid) continue;
                RequireCollider(collider);

                if (seen.Add(collider)) colliders.Add(collider);
            }

            return colliders.AsReadOnly();
        }

        private bool Owns(Collider3D collider) =>
            collider != null && ReferenceEquals(collider.Space, this) && collider.IsValid;

        private void RequireCollider(Collider3D collider)
        {
            if (!Owns(collider))
                throw new InvalidOperationException("The collider must be valid and belong to this space.");
        }

        private T RequireCreated<T>(T collider) where T : Collider3D
        {
            if (Owns(collider)) return collider;

            // The caller never receives this handle, so release what the factory made.
            collider?.Dispose();
            throw new InvalidOperationException("The created collider must be valid and belong to this space.");
        }

        private void Receive(CollisionNotification3D notification)
        {
            if (IsDisposed) return;
            _notifications.Enqueue(notification);
        }

        private static List<CollisionNotification3D> ReconcileRelations(SpatialHandle[] handles)
        {
            var exits = new List<CollisionNotification3D>();
            foreach (Collider3D collider in handles)
            {
                foreach (var relation in collider.Relations.Values)
                {
                    // Both sides keep the relation; settle it once.
                    if (!ReferenceEquals(relation.Pair.First, collider) || relation.Holds) continue;

                    exits.Add(new CollisionNotification3D(
                        relation.Pair,
                        relation.IsTrigger,
                        false,
                        relation.RelationshipId));
                }
            }

            // End the old kind before the engine can announce its replacement.
            return exits;
        }

        private void Dispatch(CollisionNotification3D notification)
        {
            var first = notification.Pair.First;
            var second = notification.Pair.Second;
            if (first == null || second == null || !ReferenceEquals(first.Space, this)
                || !ReferenceEquals(second.Space, this) || notification.RelationshipId <= 0)
            {
                SpatialDispatch.Report(
                    new InvalidOperationException("The backend supplied an invalid relationship."));
                return;
            }

            if (!first.IsValid || !second.IsValid) return;
            var trigger = notification.IsTrigger;
            if (notification.IsEntering)
            {
                if (!notification.Holds || !first.Relations.TryAdd((second, trigger), notification)) return;

                second.Relations.Add((first, trigger), notification);
            }
            else
            {
                if (!first.Relations.TryGetValue((second, trigger), out var current)
                    || current.RelationshipId != notification.RelationshipId) return;

                first.Relations.Remove((second, trigger));
                second.Relations.Remove((first, trigger));
            }

            var handlers = trigger
                ? (notification.IsEntering ? _triggerEnter : _triggerExit)
                : (notification.IsEntering ? _collisionEnter : _collisionExit);
            SpatialDispatch.Invoke(handlers, notification.Pair,
                () => IsValid && first.IsValid && second.IsValid,
                (handler, exception) =>
                {
                    Unsubscribe(notification, handler);
                    SpatialDispatch.Report(exception);
                });
            first.Publish(notification);
            second.Publish(notification);
        }

        // A world listener hears every pair, so its failure ends the subscription and not the pair at hand.
        private void Unsubscribe(CollisionNotification3D notification, Action<CollisionPair3D> handler)
        {
            if (notification.IsTrigger)
            {
                if (notification.IsEntering) _triggerEnter -= handler;
                else _triggerExit -= handler;
            }
            else
            {
                if (notification.IsEntering) _collisionEnter -= handler;
                else _collisionExit -= handler;
            }
        }

        protected override void RemoveRelations(SpatialHandle handle)
        {
            var collider = (Collider3D)handle;
            foreach (var (other, trigger) in collider.Relations.Keys) other.Relations.Remove((collider, trigger));
            collider.Relations.Clear();
        }

        protected override void OnDispose()
        {
            var unsubscribe = _unsubscribe;
            _unsubscribe = null;
            _bindings = null;
            _notifications.Clear();
            _collisionEnter = null;
            _collisionExit = null;
            _triggerEnter = null;
            _triggerExit = null;
            unsubscribe?.Invoke();
            base.OnDispose();
        }
    }
}
