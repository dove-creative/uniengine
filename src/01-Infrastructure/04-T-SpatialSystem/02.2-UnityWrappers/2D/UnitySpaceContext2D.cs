#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Objects;
using UnityEngine.SceneManagement;
using N = System.Numerics;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    internal partial class UnitySpaceContext2D
    {
        // Front
        public Scene OwnerScene { get; }
        public U.PhysicsScene2D Scene { get; }
        public CollisionSpace2D Space { get; private set; }

        // Internal
        private bool _released;
        private long _nextRelationship;
        private readonly List<(UnityObjectReference Other, bool Trigger)> _ended = new();
        private Action<CollisionNotification2D> _publish;
        private readonly List<Action> _notifications = new();
        private readonly Dictionary<int, Collider2D> _colliders = new();
        private readonly Dictionary<int, UnityObjectReference> _colliderSources = new();
        private readonly Dictionary<int, U.Object> _retired = new();
        private readonly Dictionary<int, U.GameObject> _colliderObjects = new();
        private readonly HashSet<U.Rigidbody2D> _internalBodies = new(UnityReferenceComparer<U.Rigidbody2D>.Instance);


        // Content
        public UnitySpaceContext2D()
        {
            if (!U.Application.isPlaying)
                throw new NotSupportedException("Owned Unity physics worlds require Play Mode.");

            OwnerScene = SceneManager.CreateScene("SpatialSystem 2D " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            Scene = U.PhysicsSceneExtensions2D.GetPhysicsScene2D(OwnerScene);
            for (var index = 0; index < _layerMasks.Length; index++) _layerMasks[index] = -1;
        }

        public bool IsValid() => !_released && OwnerScene.IsValid() && OwnerScene.isLoaded && Scene.IsValid();

        public bool Belongs(U.Component source) => source && source.gameObject.scene == OwnerScene;

        public void Attach(CollisionSpace2D space)
        {
            Space = space;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }

        private void SceneUnloaded(Scene scene)
        {
            if (scene == OwnerScene) Space.Dispose();
        }

        public CollisionSpaceBindings2D CreateBindings() => new(
            IsValid, RaycastAll, OverlapBox, OverlapCircle, OverlapCapsule,
            callback => { _publish = callback; return () => _publish = null; },
            CreateWorldBindings(), Release);

        public void Prune()
        {
            if (_released || !Space.IsValid) return;
            var handles = new List<SpatialHandle>();
            handles.AddRange(_colliders.Values);
            var cleanup = new List<Action>();
            foreach (var handle in handles) cleanup.Add(() => { _ = handle.IsValid; });
            foreach (var action in cleanup) action();
            foreach (var pair in new List<KeyValuePair<int, U.Object>>(_retired))
            {
                if (!pair.Value) _retired.Remove(pair.Key);
            }
            _internalBodies.RemoveWhere(body => !body);
        }

        private bool Retired(U.Object source) => source && _retired.TryGetValue(
            source.GetInstanceID(),
            out var previous)
            && ReferenceEquals(previous, source);

        private Collider2D WrapOwned(U.Collider2D source)
        {
            RequireSource(source);
            if (!_colliderObjects.TryGetValue(source.GetInstanceID(), out var owned) || owned != source.gameObject)
                throw new InvalidOperationException("Only system-created colliders can be bound.");
            if (Retired(source)) throw new ObjectDisposedException("Collider");
            ValidateTrigger(source);
            var id = source.GetInstanceID();
            if (_colliders.TryGetValue(id, out var existing))
            {
                if (_colliderSources.TryGetValue(id, out var cached)
                    && ReferenceEquals(cached.Value, source) && existing.IsValid) return existing;
                existing.Dispose();
            }
            EnsureDriver(source);
            ApplyLayerPolicy(source);
            var reference = new UnityObjectReference(source);
            var handle = UnityColliderFactory2D.Create(this, reference);
            _colliders[id] = handle;
            _colliderSources[id] = reference;
            try
            {
                reference.Connect(UnityCollisionRelay2D.Attach(source.gameObject, this));
                return handle;
            }
            catch { handle.Dispose(); throw; }
        }

        private void RequireSource(U.Component source)
        {
            if (!Space.IsValid) throw new ObjectDisposedException(nameof(Space));
            if (!source) throw new ArgumentNullException(nameof(source));
            if (!Belongs(source))
                throw new ArgumentException("Only objects in this owned world can be registered.", nameof(source));
        }

        public void ValidateTrigger(U.Collider2D source, bool becomingTrigger = false)
        {
            if (!source.isTrigger && !becomingTrigger) return;

            if (source.GetComponents<U.Collider2D>().Length != 1)
                throw new NotSupportedException(
                    "Automatic trigger delivery requires one collider per trigger GameObject.");

            var body = source.attachedRigidbody;

            if (body && body.gameObject == source.gameObject)
            {
                foreach (var child in body.GetComponentsInChildren<U.Collider2D>(true))
                {
                    if (child != source && child.attachedRigidbody == body)
                        throw new NotSupportedException(
                            "Place compound-body triggers on separate child GameObjects without the owning Rigidbody.");
                }
            }
        }

        public void Trigger(U.GameObject host, U.Collider2D other, bool entering)
        {
            if (_released || !other) return;

            // Only the sensor origin reports the pair; body-level duplicate messages are ignored.
            foreach (var source in host.GetComponents<U.Collider2D>())
            {
                if (!source || !source.isTrigger || !Belongs(source)) continue;

                if (other.isTrigger && source.GetInstanceID() > other.GetInstanceID()) continue;
                Report(source, other, true, entering);
            }
        }

        public void Contact(U.GameObject host, U.Collider2D first, U.Collider2D second, bool entering)
        {
            if (!first || !second) return;
            var owner = first.GetInstanceID() < second.GetInstanceID() ? first : second;
            if (owner.gameObject == host) Report(first, second, false, entering);
        }

        public void Report(U.Collider2D first, U.Collider2D second, bool trigger, bool entering)
        {
            if (_released || !first || !second || first == second) return;
            if (!Belongs(first) || !Belongs(second) || Retired(first) || Retired(second)) return;
            if (!_colliders.TryGetValue(first.GetInstanceID(), out var firstHandle)
                || !_colliders.TryGetValue(second.GetInstanceID(), out var secondHandle)) return;
            var firstSource = _colliderSources[first.GetInstanceID()];
            var secondSource = _colliderSources[second.GetInstanceID()];
            if (!ReferenceEquals(firstSource.Value, first) || !ReferenceEquals(secondSource.Value, second)) return;
            if (!firstSource.Relations.TryGetValue((secondSource, trigger), out var relationship))
            {
                if (!entering) return;

                relationship = ++_nextRelationship;
                firstSource.StartRelation(secondSource, trigger, relationship);
            }
            if (!entering) firstSource.EndRelation(secondSource, trigger);
            var notification = new CollisionNotification2D(
                new CollisionPair2D(firstHandle, secondHandle), trigger, entering, relationship);
            _notifications.Add(() => _publish?.Invoke(notification));
        }

        private void PrepareCollider(UnityObjectReference reference)
        {
            var source = (U.Collider2D)reference.Value;
            ValidateTrigger(source);
            ApplyLayerPolicy(source);
            var inactive = !source.enabled || !source.gameObject.activeInHierarchy
                || (source.attachedRigidbody && !source.attachedRigidbody.simulated);
            _ended.Clear();
            foreach (var relation in reference.Relations.Keys)
            {
                var other = relation.Other.Value as U.Collider2D;
                if (inactive || !other || relation.Trigger != (source.isTrigger || other.isTrigger))
                    _ended.Add(relation);
            }
            foreach (var (other, trigger) in _ended) reference.EndRelation(other, trigger);
        }

        private Collider2D FindRegistered(U.Collider2D source)
        {
            if (!source || Retired(source)) return null;
            var id = source.GetInstanceID();
            if (!_colliderSources.TryGetValue(id, out var reference) || !ReferenceEquals(reference.Value, source))
                throw new InvalidOperationException("An unregistered collider was inserted into the owned world.");
            return _colliders.TryGetValue(id, out var handle) && handle.IsValid ? handle : null;
        }

        private void ReleaseCollider(UnityObjectReference reference)
        {
            var source = reference.Value as U.Collider2D;
            reference.EndRelations();
            if (_colliderSources.TryGetValue(reference.Id, out var current) && ReferenceEquals(current, reference))
            {
                _colliderSources.Remove(reference.Id);
                _colliders.Remove(reference.Id);
            }
            var cleanup = new List<Action> { reference.Release };
            _colliderObjects.TryGetValue(reference.Id, out var host);
            _colliderObjects.Remove(reference.Id);
            if (source)
            {
                _retired[reference.Id] = source;
                cleanup.Add(() => { if (source) source.enabled = false; });
                if (host && source.attachedRigidbody && source.attachedRigidbody.gameObject == host
                    && _internalBodies.Contains(source.attachedRigidbody))
                    _internalBodies.Remove(source.attachedRigidbody);
            }
            if (host) cleanup.Add(() =>
            {
                if (!host) return;
                host.SetActive(false);
                UnityBindingUtility.DestroyOwned(host);
            });
            else if (source) cleanup.Add(() => UnityBindingUtility.DestroyOwned(source));
            UnityBindingUtility.RunAll(cleanup);
        }

        private void Release()
        {
            if (_released) return;
            _released = true;
            _publish = null;
            _notifications.Clear();
            SceneManager.sceneUnloaded -= SceneUnloaded;

            var cleanup = new List<Action>();
            foreach (var reference in _colliderSources.Values) cleanup.Add(reference.Release);
            _colliders.Clear();
            _colliderSources.Clear();
            _colliderObjects.Clear();
            _internalBodies.Clear();
            _retired.Clear();
            cleanup.Add(() =>
            {
                if (OwnerScene.IsValid() && OwnerScene.isLoaded) SceneManager.UnloadSceneAsync(OwnerScene);
            });
            UnityBindingUtility.RunAll(cleanup);
        }

        public ColliderBindings2D ColliderBindings(UnityObjectReference reference)
        {
            U.Collider2D Source() => (U.Collider2D)reference.Value;
            return new ColliderBindings2D(
                UnityBindingUtility.Property<Transform2D>(
                    () => { var source = Source(); return UnityBindingUtility.Pose2D(source.transform); },
                    value => { var source = Source(); UnityBindingUtility.SetPose2D(source.transform, value); }),
                UnityBindingUtility.Property<bool>(() => Source().enabled, value =>
                {
                    var source = Source();
                    source.enabled = value;
                }),
                UnityBindingUtility.Property<bool>(() => Source().isTrigger, value =>
                {
                    var source = Source();
                    if (value) ValidateTrigger(source, true);
                    if (source.isTrigger == value) return;
                    source.isTrigger = value;
                }),
                UnityBindingUtility.Property<int>(() => Source().gameObject.layer, value =>
                {
                    Source().gameObject.layer = value;
                    ApplyLayerPolicy(Source());
                }),
                () => IsValid() && Source() && Belongs(Source()),
                () => ReleaseCollider(reference),
                () => Source().gameObject.activeInHierarchy
                    && (!Source().attachedRigidbody || Source().attachedRigidbody.simulated),
                () => UnityBindingUtility.Engine(() => PrepareCollider(reference)));
        }

        private IReadOnlyList<SpatialRaycastHit2D> RaycastAll(
            N.Vector2 origin, N.Vector2 direction, float distance, SpatialQueryFilter filter)
        {
            var native = UnityBindingUtility.QueryAll<U.RaycastHit2D>(buffer =>
                Scene.Raycast(UnityBindingUtility.Native(origin), UnityBindingUtility.Native(direction),
                    distance, UnityBindingUtility.Filter(filter), buffer));
            var result = new List<SpatialRaycastHit2D>(native.Length);

            foreach (var hit in native)
            {
                var handle = FindRegistered(hit.collider);
                if (handle != null)
                    result.Add(new SpatialRaycastHit2D(handle,
                        UnityBindingUtility.Common(hit.point), UnityBindingUtility.Common(hit.normal), hit.distance));
            }

            return result;
        }

        private IReadOnlyList<Collider2D> Convert(U.Collider2D[] sources)
        {
            var result = new List<Collider2D>(sources.Length);

            foreach (var source in sources)
            {
                var handle = FindRegistered(source);
                if (handle != null) result.Add(handle);
            }

            return result;
        }

        private IReadOnlyList<Collider2D> OverlapBox(Transform2D pose, N.Vector2 size, SpatialQueryFilter filter) =>
            Convert(UnityBindingUtility.QueryAll<U.Collider2D>(buffer =>
                Scene.OverlapBox(UnityBindingUtility.Native(pose.Position), UnityBindingUtility.Native(size),
                    pose.Rotation, UnityBindingUtility.Filter(filter), buffer)));

        private IReadOnlyList<Collider2D> OverlapCircle(N.Vector2 center, float radius, SpatialQueryFilter filter) =>
            Convert(UnityBindingUtility.QueryAll<U.Collider2D>(buffer =>
                Scene.OverlapCircle(
                    UnityBindingUtility.Native(center),
                    radius,
                    UnityBindingUtility.Filter(filter),
                    buffer)));

        private IReadOnlyList<Collider2D> OverlapCapsule(
            Transform2D pose, N.Vector2 size, CapsuleDirection2D direction, SpatialQueryFilter filter) =>
            Convert(UnityBindingUtility.QueryAll<U.Collider2D>(buffer =>
                Scene.OverlapCapsule(UnityBindingUtility.Native(pose.Position), UnityBindingUtility.Native(size),
                    UnityBindingUtility.Native(direction), pose.Rotation, UnityBindingUtility.Filter(filter), buffer)));

    }
}
#endif
