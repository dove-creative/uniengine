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
        // Internal
        private readonly int[] _layerMasks = new int[32];


        // Content
        private CollisionWorldBindings3D CreateWorldBindings() => new(
            Update, (first, second) => (_layerMasks[first] & (1 << second)) != 0, SetLayerCollision,
            (pose, size) => (BoxCollider3D)Create<U.BoxCollider>(pose,
                source => source.size = UnityBindingUtility.Native(size)),
            (pose, radius) => (SphereCollider3D)Create<U.SphereCollider>(pose,
                source => source.radius = radius),
            (pose, radius, height, direction) => (CapsuleCollider3D)Create<U.CapsuleCollider>(pose, source =>
            {
                source.radius = radius;
                source.height = height;
                source.direction = (int)direction;
            }));

        private void SetLayerCollision(int first, int second, bool enabled)
        {
            if (enabled)
            {
                _layerMasks[first] |= 1 << second;
                _layerMasks[second] |= 1 << first;
            }
            else
            {
                _layerMasks[first] &= ~(1 << second);
                _layerMasks[second] &= ~(1 << first);
            }
        }

        private void ApplyLayerPolicy(U.Collider source)
        {
            var mask = _layerMasks[source.gameObject.layer];
            var changed = source.includeLayers.value != mask || source.excludeLayers.value != ~mask
                || source.layerOverridePriority != 1;
            if (!changed) return;

            source.includeLayers = mask;
            source.excludeLayers = ~mask;
            source.layerOverridePriority = 1;

            if (changed && source.attachedRigidbody) source.attachedRigidbody.WakeUp();
        }

        private static void DisableNativeGravity(U.Rigidbody body)
        {
            body.useGravity = false;
            body.includeLayers = 0;
            body.excludeLayers = 0;
        }

        private void EnsureDriver(U.Collider source)
        {
            var body = source.attachedRigidbody;

            if (body)
            {
                if (!_internalBodies.Contains(body))
                {
                    if (body.gameObject != source.gameObject)
                        throw new InvalidOperationException(
                            "Only internal collision drivers may be shared by child colliders.");

                    _internalBodies.Add(body);
                    ConfigureDriver(body);
                }

                return;
            }

            // Complex static geometry keeps Unity's native contact restrictions.
            if (!(source is U.BoxCollider) && !(source is U.SphereCollider) && !(source is U.CapsuleCollider)) return;
            body = source.gameObject.AddComponent<U.Rigidbody>();
            _internalBodies.Add(body);
            ConfigureDriver(body);
        }

        private static void ConfigureDriver(U.Rigidbody body)
        {
            DisableNativeGravity(body);
            body.isKinematic = false;
            body.constraints = U.RigidbodyConstraints.FreezeAll;
            body.sleepThreshold = 0f;
        }

        public Collider3D CreateUnityCollider<T>(Transform3D pose) where T : U.Collider
        {
            return Create<T>(pose, null);
        }

        private Collider3D Create<T>(Transform3D pose, Action<T> configure) where T : U.Collider
        {
            if (!Space.IsValid) throw new ObjectDisposedException(nameof(Space));
            var host = NewObject(typeof(T).Name);

            try
            {
                UnityBindingUtility.SetPose3D(host.transform, pose);
                var source = host.AddComponent<T>();
                configure?.Invoke(source);
                _colliderObjects[source.GetInstanceID()] = host;
                return WrapOwned(source);
            }
            catch
            {
                foreach (var component in host.GetComponents<U.Collider>())
                    _colliderObjects.Remove(component.GetInstanceID());

                host.SetActive(false);
                UnityBindingUtility.DestroyOwned(host);
                throw;
            }
        }

        private U.GameObject NewObject(string name)
        {
            var host = new U.GameObject(name);
            SceneManager.MoveGameObjectToScene(host, OwnerScene);
            return host;
        }

        private void Update(float deltaTime)
        {
            Prune();
            if (!Space.IsValid) throw new ObjectDisposedException(nameof(Space));

            UnityBindingUtility.Engine(() => Scene.Simulate(deltaTime));
            var pending = _notifications.ToArray();
            _notifications.Clear();
            var callbacks = new List<Action>();

            foreach (var callback in pending)
            {
                var captured = callback;
                callbacks.Add(() => { if (!_released && Space.IsValid) captured(); });
            }

            foreach (var callback in callbacks) callback();
        }
    }
}
#endif
