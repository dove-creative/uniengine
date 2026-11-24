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
        // Internal
        private readonly int[] _layerMasks = new int[32];


        // Content
        private CollisionWorldBindings2D CreateWorldBindings() => new(
            Update, (first, second) => (_layerMasks[first] & (1 << second)) != 0, SetLayerCollision,
            (pose, size) => (BoxCollider2D)Create<U.BoxCollider2D>(pose,
                source => source.size = UnityBindingUtility.Native(size)),
            (pose, radius) => (CircleCollider2D)Create<U.CircleCollider2D>(pose,
                source => source.radius = radius),
            (pose, size, direction) => (CapsuleCollider2D)Create<U.CapsuleCollider2D>(pose, source =>
            {
                source.size = UnityBindingUtility.Native(size);
                source.direction = UnityBindingUtility.Native(direction);
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

        private void ApplyLayerPolicy(U.Collider2D source)
        {
            var mask = _layerMasks[source.gameObject.layer];
            var changed = source.includeLayers.value != mask || source.excludeLayers.value != ~mask
                || source.layerOverridePriority != 1 || source.forceSendLayers.value != -1
                || source.forceReceiveLayers.value != -1 || source.contactCaptureLayers.value != -1
                || source.callbackLayers.value != -1;
            if (!changed) return;

            source.includeLayers = mask;
            source.excludeLayers = ~mask;
            source.layerOverridePriority = 1;
            source.forceSendLayers = -1;
            source.forceReceiveLayers = -1;
            source.contactCaptureLayers = -1;
            source.callbackLayers = -1;
            if (changed && source.attachedRigidbody) source.attachedRigidbody.WakeUp();
        }

        private static void DisableNativeGravity(U.Rigidbody2D body)
        {
            body.gravityScale = 0f;
            body.includeLayers = 0;
            body.excludeLayers = 0;
        }

        private void EnsureDriver(U.Collider2D source)
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


            body = source.gameObject.AddComponent<U.Rigidbody2D>();
            _internalBodies.Add(body);
            ConfigureDriver(body);
        }

        private static void ConfigureDriver(U.Rigidbody2D body)
        {
            DisableNativeGravity(body);
            body.bodyType = U.RigidbodyType2D.Dynamic;
            body.constraints = U.RigidbodyConstraints2D.FreezeAll;
            body.sleepMode = U.RigidbodySleepMode2D.NeverSleep;
        }

        public Collider2D CreateUnityCollider<T>(Transform2D pose) where T : U.Collider2D
        {
            // It has no shape of its own, and this system offers no way to add the colliders it merges.
            if (typeof(T) == typeof(U.CompositeCollider2D))
                throw new NotSupportedException("CompositeCollider2D is not supported.");

            return Create<T>(pose, null);
        }

        private Collider2D Create<T>(Transform2D pose, Action<T> configure) where T : U.Collider2D
        {
            if (!Space.IsValid) throw new ObjectDisposedException(nameof(Space));
            var host = NewObject(typeof(T).Name);

            try
            {
                UnityBindingUtility.SetPose2D(host.transform, pose);
                var source = host.AddComponent<T>();
                configure?.Invoke(source);
                _colliderObjects[source.GetInstanceID()] = host;
                return WrapOwned(source);
            }
            catch
            {
                foreach (var component in host.GetComponents<U.Collider2D>())
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

            if (!UnityBindingUtility.Engine(() => Scene.Simulate(deltaTime)))
                throw new SpatialEngineException("Unity did not advance the local 2D physics world.",
                    new InvalidOperationException("PhysicsScene2D.Simulate returned false."));
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
