#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Objects;
using UnityEngine.SceneManagement;
using N = System.Numerics;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    internal partial class UnitySpaceContext3D
    {
        // Front
        public Scene OwnerScene { get; }
        public U.PhysicsScene Scene { get; }
        public CollisionSpace3D Space { get; private set; }

        // Internal
        private bool _released;
        private long _nextRelationship;
        private readonly List<(UnityObjectReference Other, bool Trigger)> _ended = new();
        private Action<CollisionNotification3D> _publish;
        private readonly List<Action> _notifications = new();
        private readonly Dictionary<int, Collider3D> _colliders = new();
        private readonly Dictionary<int, UnityObjectReference> _colliderSources = new();
        private readonly Dictionary<int, U.Object> _retired = new();
        private readonly Dictionary<int, U.GameObject> _colliderObjects = new();
        private readonly HashSet<U.Rigidbody> _internalBodies = new(UnityReferenceComparer<U.Rigidbody>.Instance);


        // Content
        public UnitySpaceContext3D()
        {
            if (!U.Application.isPlaying)
                throw new NotSupportedException("Owned Unity physics worlds require Play Mode.");

            OwnerScene = SceneManager.CreateScene("SpatialSystem 3D " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            Scene = U.PhysicsSceneExtensions.GetPhysicsScene(OwnerScene);
            for (var index = 0; index < _layerMasks.Length; index++) _layerMasks[index] = -1;
        }

        public bool IsValid() => !_released && OwnerScene.IsValid() && OwnerScene.isLoaded && Scene.IsValid();

        public bool Belongs(U.Component source) => source && source.gameObject.scene == OwnerScene;

        public void Attach(CollisionSpace3D space)
        {
            Space = space;
            SceneManager.sceneUnloaded += SceneUnloaded;
            U.Physics.ContactEvent += Contacts;
        }

        private void SceneUnloaded(Scene scene)
        {
            if (scene == OwnerScene) Space.Dispose();
        }

        public CollisionSpaceBindings3D CreateBindings() => new(
            IsValid, RaycastAll, OverlapBox, OverlapSphere, OverlapCapsule,
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

        private Collider3D WrapOwned(U.Collider source)
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
            var handle = UnityColliderFactory3D.Create(this, reference);
            _colliders[id] = handle;
            _colliderSources[id] = reference;
            try
            {
                reference.Connect(UnityCollisionRelay3D.Attach(source.gameObject, this));
                source.providesContacts = true;
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

        public void ValidateTrigger(U.Collider source, bool becomingTrigger = false)
        {
            if (!source.isTrigger && !becomingTrigger) return;

            if (source.GetComponents<U.Collider>().Length != 1)
                throw new NotSupportedException(
                    "Automatic trigger delivery requires one collider per trigger GameObject.");

            var body = source.attachedRigidbody;

            if (body && body.gameObject == source.gameObject)
            {
                foreach (var child in body.GetComponentsInChildren<U.Collider>(true))
                {
                    if (child != source && child.attachedRigidbody == body)
                        throw new NotSupportedException(
                            "Place compound-body triggers on separate child GameObjects without the owning Rigidbody.");
                }
            }
        }

        public void Trigger(U.GameObject host, U.Collider other, bool entering)
        {
            if (_released || !other) return;

            // Only the sensor origin reports the pair; body-level duplicate messages are ignored.
            foreach (var source in host.GetComponents<U.Collider>())
            {
                if (!source || !source.isTrigger || !Belongs(source)) continue;

                if (other.isTrigger && source.GetInstanceID() > other.GetInstanceID()) continue;
                Report(source, other, true, entering);
            }
        }

        public void Report(U.Collider first, U.Collider second, bool trigger, bool entering)
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
            var notification = new CollisionNotification3D(
                new CollisionPair3D(firstHandle, secondHandle), trigger, entering, relationship);
            _notifications.Add(() => _publish?.Invoke(notification));
        }

        private void PrepareCollider(UnityObjectReference reference)
        {
            var source = (U.Collider)reference.Value;
            ValidateTrigger(source);
            ApplyLayerPolicy(source);
            var inactive = !source.enabled || !source.gameObject.activeInHierarchy;
            _ended.Clear();
            foreach (var relation in reference.Relations.Keys)
            {
                var other = relation.Other.Value as U.Collider;
                if (inactive || !other || relation.Trigger != (source.isTrigger || other.isTrigger))
                    _ended.Add(relation);
            }
            foreach (var (other, trigger) in _ended) reference.EndRelation(other, trigger);
        }

        private Collider3D FindRegistered(U.Collider source)
        {
            if (!source || Retired(source)) return null;
            var id = source.GetInstanceID();
            if (!_colliderSources.TryGetValue(id, out var reference) || !ReferenceEquals(reference.Value, source))
                throw new InvalidOperationException("An unregistered collider was inserted into the owned world.");
            return _colliders.TryGetValue(id, out var handle) && handle.IsValid ? handle : null;
        }

        private void ReleaseCollider(UnityObjectReference reference)
        {
            var source = reference.Value as U.Collider;
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
            U.Physics.ContactEvent -= Contacts;
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

        public ColliderBindings3D ColliderBindings(UnityObjectReference reference)
        {
            U.Collider Source() => (U.Collider)reference.Value;
            return new ColliderBindings3D(
                UnityBindingUtility.Property<Transform3D>(
                    () => { var source = Source(); return UnityBindingUtility.Pose3D(source.transform); },
                    value => { var source = Source(); UnityBindingUtility.SetPose3D(source.transform, value); }),
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
                () => Source().gameObject.activeInHierarchy,
                () => UnityBindingUtility.Engine(() => PrepareCollider(reference)));
        }

        private IReadOnlyList<SpatialRaycastHit3D> RaycastAll(
            N.Vector3 origin, N.Vector3 direction, float distance, SpatialQueryFilter filter)
        {
            var native = UnityBindingUtility.QueryAll<U.RaycastHit>(buffer =>
                Scene.Raycast(UnityBindingUtility.Native(origin), UnityBindingUtility.Native(direction),
                    buffer, distance, filter.LayerMask, UnityBindingUtility.TriggerFilter(filter)));
            var result = new List<SpatialRaycastHit3D>(native.Length);

            foreach (var hit in native)
            {
                var handle = FindRegistered(hit.collider);
                if (handle != null)
                    result.Add(new SpatialRaycastHit3D(handle,
                        UnityBindingUtility.Common(hit.point), UnityBindingUtility.Common(hit.normal), hit.distance));
            }

            return result;
        }

        private IReadOnlyList<Collider3D> Convert(U.Collider[] sources)
        {
            var result = new List<Collider3D>(sources.Length);

            foreach (var source in sources)
            {
                var handle = FindRegistered(source);
                if (handle != null) result.Add(handle);
            }

            return result;
        }

        private IReadOnlyList<Collider3D> OverlapBox(Transform3D pose, N.Vector3 size, SpatialQueryFilter filter) =>
            Convert(UnityBindingUtility.QueryAll<U.Collider>(buffer =>
                Scene.OverlapBox(UnityBindingUtility.Native(pose.Position), UnityBindingUtility.Native(size * 0.5f),
                    buffer, UnityBindingUtility.Native(pose.Rotation),
                    filter.LayerMask, UnityBindingUtility.TriggerFilter(filter))));

        private IReadOnlyList<Collider3D> OverlapSphere(N.Vector3 center, float radius, SpatialQueryFilter filter) =>
            Convert(UnityBindingUtility.QueryAll<U.Collider>(buffer =>
                Scene.OverlapSphere(UnityBindingUtility.Native(center), radius, buffer,
                    filter.LayerMask, UnityBindingUtility.TriggerFilter(filter))));

        private IReadOnlyList<Collider3D> OverlapCapsule(
            N.Vector3 firstCenter, N.Vector3 secondCenter, float radius, SpatialQueryFilter filter) =>
            Convert(UnityBindingUtility.QueryAll<U.Collider>(buffer =>
                Scene.OverlapCapsule(UnityBindingUtility.Native(firstCenter), UnityBindingUtility.Native(secondCenter),
                    radius, buffer, filter.LayerMask, UnityBindingUtility.TriggerFilter(filter))));


        private void Contacts(
            U.PhysicsScene scene,
            global::Unity.Collections.NativeArray<U.ContactPairHeader>.ReadOnly headers)
        {
            if (_released || !scene.Equals(Scene)) return;

            var notifications = new List<Action>();

            for (var headerIndex = 0; headerIndex < headers.Length; headerIndex++)
            {
                var header = headers[headerIndex];
#if UNITY_6000_0_OR_NEWER
                var count = header.pairCount;
#else
                var count = header.PairCount;
#endif
                for (var pairIndex = 0; pairIndex < count; pairIndex++)
                {
                    var pair = header.GetContactPair(pairIndex);
#if UNITY_6000_0_OR_NEWER
                    var first = pair.collider;
                    var second = pair.otherCollider;
                    var entering = pair.isCollisionEnter;
                    var exiting = pair.isCollisionExit;
#else
                    var first = pair.Collider;
                    var second = pair.OtherCollider;
                    var entering = pair.IsCollisionEnter;
                    var exiting = pair.IsCollisionExit;
#endif
                    if (entering) notifications.Add(() => Report(first, second, false, true));
                    if (exiting) notifications.Add(() => Report(first, second, false, false));
                }
            }

            UnityBindingUtility.RunAll(notifications);
        }
    }
}
#endif
