#if UNITY_2022_3_OR_NEWER && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using BlackThunder.UniEngine.Objects;
using BlackThunder.UniEngine.SpatialSystem;
using BlackThunder.UniEngine.SpatialSystem.Unity;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using N = System.Numerics;
using U = UnityEngine;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture, Category("SpatialSystemUnity")]
    public class SpatialUnity2DTests
    {
        private CollisionSpace2DUnity _space;
        private readonly List<SpatialSpace> _worlds = new();
        private readonly List<Scene> _scenes = new();


        [SetUp]
        public void SetUp()
        {
            Assert.That(U.Application.isPlaying, Is.True, "Run in a Unity PlayMode test assembly.");
            _space = CollisionWorld();
        }

        private CollisionSpace2DUnity CollisionWorld()
        {
            var world = new CollisionSpace2DUnity();
            _worlds.Add(world);
            var probe = (BoxCollider2DUnity)world.CreateBoxCollider(N.Vector2.One, Pose(10000f));
            _scenes.Add(probe.Source.gameObject.scene);
            probe.Dispose();
            return world;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var world in _worlds) world.Dispose();
            for (var frame = 0; frame < 100 && _scenes.Exists(scene => scene.IsValid() && scene.isLoaded); frame++)
                yield return null;
            Assert.That(_scenes.Exists(scene => scene.IsValid() && scene.isLoaded), Is.False);
            _worlds.Clear();
            _scenes.Clear();
        }

        private static Transform2D Pose(float x = 0f) => new(N.Vector2.UnitX * x, 0f);

        private static Scene SceneOf(CollisionSpace2DUnity world)
        {
            var probe = (BoxCollider2DUnity)world.CreateBoxCollider(N.Vector2.One, Pose(10000f));
            var scene = probe.Source.gameObject.scene;
            probe.Dispose();
            return scene;
        }

        [Test]
        public void FactoriesSelectEverySupportedWrapperAndPreserveIdentity()
        {
            var box = _space.CreateBoxCollider(N.Vector2.One * 2f);
            var ball = _space.CreateCircleCollider(2f, Pose(10f));
            var capsule = _space.CreateCapsuleCollider(new N.Vector2(1f, 2f), pose: Pose(20f));
            Assert.That(box, Is.TypeOf<BoxCollider2DUnity>());
            Assert.That(ball, Is.TypeOf<CircleCollider2DUnity>());
            Assert.That(capsule, Is.TypeOf<CapsuleCollider2DUnity>());
            Assert.That(
                _space.CreateUnityCollider<U.PolygonCollider2D>(Pose(30f)),
                Is.TypeOf<PolygonCollider2DUnity>());
            Assert.That(_space.CreateUnityCollider<U.EdgeCollider2D>(Pose(40f)), Is.TypeOf<EdgeCollider2DUnity>());
            Assert.That(_space.CreateUnityCollider<U.CustomCollider2D>(Pose(50f)), Is.TypeOf<Collider2DUnity>());
            var source = ((BoxCollider2DUnity)box).Source;
            box.Layer = 8;
            box.Enabled = false;
            _space.Update(0.02f);
            Assert.That(source.gameObject.layer, Is.EqualTo(8));
            Assert.That(source.enabled, Is.False);
            Assert.That(source.size, Is.EqualTo(U.Vector2.one * 2f));
            Assert.That(source.gameObject.scene, Is.EqualTo(SceneOf(_space)));
            Assert.That(source.GetComponent<U.Renderer>(), Is.Null);
        }

        [Test]
        public void QueriesGrowAndWorldsAtIdenticalCoordinatesRemainIsolated()
        {
            for (var index = 0; index < 40; index++)
            {
                var collider = _space.CreateBoxCollider(N.Vector2.One, Pose(index * 2f));
                collider.Layer = 8;
                collider.IsTrigger = index % 2 == 0;
            }
            var other = CollisionWorld();
            var foreign = (BoxCollider2DUnity)other.CreateBoxCollider(N.Vector2.One);
            foreign.Layer = 8;
            _space.Update(0.02f);
            other.Update(0.02f);
            Assert.That(SceneOf(_space), Is.Not.EqualTo(SceneOf(other)));
            var all = _space.OverlapCircle(N.Vector2.Zero, 100f, new SpatialQueryFilter(1 << 8, true));
            Assert.That(all.Count, Is.EqualTo(40));
            Assert.That(
                _space.OverlapCircle(N.Vector2.Zero, 100f, new SpatialQueryFilter(1 << 8, false)).Count,
                Is.EqualTo(20));
            Assert.That(_space.OverlapCircle(N.Vector2.Zero, 100f, new SpatialQueryFilter(0, true)), Is.Empty);
            Assert.That(_space.RaycastAll(-N.Vector2.UnitX * 2f, N.Vector2.UnitX, 100f,
                new SpatialQueryFilter(1 << 8, true)).Count, Is.EqualTo(40));
            Assert.That(other.OverlapCircle(N.Vector2.Zero, 100f).Count, Is.EqualTo(1));
            foreach (var handle in all) Assert.That(handle.Space, Is.SameAs(_space));
        }

        [UnityTest]
        public IEnumerator EventsAreDeliveredByTheSelectedWorldUpdate()
        {
            var other = CollisionWorld();
            var firstEvents = 0;
            var secondEvents = 0;
            _space.TriggerEnter += _ => firstEvents++;
            other.TriggerEnter += _ => secondEvents++;

            foreach (var world in new[] { _space, other })
            {
                world.CreateBoxCollider(N.Vector2.One).IsTrigger = true;
                world.CreateBoxCollider(N.Vector2.One, Pose(0.5f));
            }

            yield return new U.WaitForFixedUpdate();
            Assert.That(firstEvents, Is.Zero);
            Assert.That(secondEvents, Is.Zero);
            _space.Update(0.01f);
            Assert.That(firstEvents, Is.EqualTo(1));
            Assert.That(secondEvents, Is.Zero);
            other.Update(0.02f);
            Assert.That(secondEvents, Is.EqualTo(1));
            Assert.That(firstEvents, Is.EqualTo(1));
        }

        [Test]
        public void LayerPoliciesAreIndependentAndKeepGlobalsUnchanged()
        {
            var globalGravity = U.Physics2D.gravity;
            var globalIgnored = U.Physics2D.GetIgnoreLayerCollision(8, 9);
            var enabled = CollisionWorld();
            var disabled = CollisionWorld();
            enabled.SetLayerCollision(8, 9, true);
            disabled.SetLayerCollision(8, 9, false);
            var enabledEnters = 0;
            var disabledEnters = 0;
            enabled.TriggerEnter += _ => enabledEnters++;
            disabled.TriggerEnter += _ => disabledEnters++;
            foreach (var world in new[] { enabled, disabled })
            {
                var sensor = world.CreateBoxCollider(N.Vector2.One);
                sensor.IsTrigger = true;
                sensor.Layer = 8;
                world.CreateBoxCollider(N.Vector2.One, Pose(0.5f)).Layer = 9;
                world.Update(0.02f);
            }
            Assert.That(enabledEnters, Is.EqualTo(1));
            Assert.That(disabledEnters, Is.Zero);
            Assert.That(disabled.GetLayerCollision(9, 8), Is.False);
            Assert.That(enabled.GetLayerCollision(9, 8), Is.True);
            Assert.That(U.Physics2D.gravity, Is.EqualTo(globalGravity));
            Assert.That(U.Physics2D.GetIgnoreLayerCollision(8, 9), Is.EqualTo(globalIgnored));
            disabled.SetLayerCollision(9, 8, true);
            disabled.Update(0.02f);
            Assert.That(disabledEnters, Is.EqualTo(1));
        }

        [TestCase(false, 0), TestCase(true, 0)]
        [TestCase(false, 1), TestCase(true, 1)]
        [TestCase(false, 2), TestCase(true, 2)]
        public void CollisionOnlyWorldAutomaticallyReportsNativeTransitions(bool trigger, int shape)
        {
            var world = CollisionWorld();
            Collider2D first = shape == 0 ? world.CreateBoxCollider(N.Vector2.One)
                : shape == 1 ? world.CreateCircleCollider(0.5f)
                : world.CreateCapsuleCollider(new N.Vector2(1f, 2f));
            first.IsTrigger = trigger;
            var second = world.CreateBoxCollider(N.Vector2.One, Pose(0.5f));
            var enters = 0;
            var exits = 0;
            var firstEvents = 0;
            var secondEvents = 0;
            void Enter(CollisionPair2D pair)
            {
                Assert.That(pair.Other(first), Is.SameAs(second));
                Assert.Throws<InvalidOperationException>(() => world.Update(0.02f));
                // Mutation is safe because delivery occurs after the native step.
                world.CreateBoxCollider(N.Vector2.One, Pose(100f)).Dispose();
                enters++;
            }
            if (trigger)
            {
                world.TriggerEnter += Enter;
                world.TriggerExit += _ => exits++;
                first.TriggerEnter += _ => firstEvents++;
                second.TriggerEnter += _ => secondEvents++;
            }
            else
            {
                world.CollisionEnter += Enter;
                world.CollisionExit += _ => exits++;
                first.CollisionEnter += _ => firstEvents++;
                second.CollisionEnter += _ => secondEvents++;
            }
            world.Update(0.02f);
            world.Update(0.02f);
            Assert.That(enters, Is.EqualTo(1));
            Assert.That(firstEvents, Is.EqualTo(1));
            Assert.That(secondEvents, Is.EqualTo(1));
            second.Pose = Pose(10f);
            world.Update(0.02f);
            Assert.That(exits, Is.EqualTo(1));
            second.Pose = Pose(0.5f);
            world.Update(0.02f);
            Assert.That(enters, Is.EqualTo(2), "Exit and reentry in one frame remain distinct.");
        }

        [Test]
        public void ReenabledSensorCanReportANewNativeEntry()
        {
            var world = CollisionWorld();
            var sensor = world.CreateBoxCollider(N.Vector2.One);
            sensor.IsTrigger = true;
            world.CreateBoxCollider(N.Vector2.One, Pose(0.5f));
            var enters = 0;
            world.TriggerEnter += _ => enters++;
            world.Update(0.02f);
            sensor.Enabled = false;
            world.Update(0.02f);
            sensor.Enabled = true;
            world.Update(0.02f);
            Assert.That(enters, Is.EqualTo(2));
        }

        [Test]
        public void DeferredDisableAndExternalInactiveStateEndRelationshipsOnce()
        {
            var sensor = (BoxCollider2DUnity)_space.CreateBoxCollider(N.Vector2.One);
            sensor.IsTrigger = true;
            _space.CreateBoxCollider(N.Vector2.One, Pose(0.5f));
            var enters = 0;
            var exits = 0;
            _space.TriggerEnter += _ => enters++;
            _space.TriggerExit += _ => exits++;
            _space.Update(0.02f);
            sensor.Enabled = false;
            sensor.Enabled = true;
            _space.Update(0.02f);
            Assert.That(enters, Is.EqualTo(1));
            Assert.That(exits, Is.Zero);
            sensor.Enabled = false;
            _space.Update(0.02f);
            _space.Update(0.02f);
            Assert.That(exits, Is.EqualTo(1));
            sensor.Enabled = true;
            _space.Update(0.02f);
            Assert.That(enters, Is.EqualTo(2));
            sensor.Source.gameObject.SetActive(false);
            _space.Update(0.02f);
            Assert.That(exits, Is.EqualTo(2));
            Assert.That(sensor.Enabled && sensor.IsValid, Is.True);
        }

        [Test]
        public void QueryResultsReuseExistingHandlesAndRejectUnregisteredObjects()
        {
            var collider = (BoxCollider2DUnity)_space.CreateBoxCollider(N.Vector2.One);
            _space.Update(0.02f);
            Assert.That(_space.OverlapCircle(N.Vector2.Zero, 2f)[0], Is.SameAs(collider));
            Assert.That(_space.OverlapCircle(N.Vector2.Zero, 2f)[0], Is.SameAs(collider));
            var injected = new U.GameObject("misuse probe");
            SceneManager.MoveGameObjectToScene(injected, collider.Source.gameObject.scene);
            injected.AddComponent<U.BoxCollider2D>();
            _space.Update(0.02f);
            Assert.Throws<InvalidOperationException>(() => _space.OverlapCircle(N.Vector2.Zero, 2f));
        }

        [Test]
        public void UnsupportedCompositeIsRejectedWithoutLeavingAnObject()
        {
            var scene = _scenes[0];
            var before = scene.rootCount;
            Assert.Throws<NotSupportedException>(() => _space.CreateUnityCollider<U.CompositeCollider2D>());
            Assert.That(scene.rootCount, Is.EqualTo(before));
            Assert.That(_space.IsValid, Is.True);
        }

        [Test]
        public void MovingOwnedObjectInvalidatesTheOriginalHandle()
        {
            var other = CollisionWorld();
            var collider = (BoxCollider2DUnity)_space.CreateBoxCollider(N.Vector2.One);
            var source = collider.Source;
            var disposed = 0;
            collider.Disposed += () => disposed++;
            SceneManager.MoveGameObjectToScene(source.gameObject, SceneOf(other));
            Assert.That(collider.IsValid, Is.False);
            Assert.That(disposed, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => _ = collider.Source);
        }

        [Test]
        public void AmbiguousTriggerLayoutsAreRejectedAndExcluded()
        {
            var sensor = (BoxCollider2DUnity)_space.CreateBoxCollider(N.Vector2.One);
            var source = sensor.Source;
            source.gameObject.AddComponent<U.BoxCollider2D>();
            sensor.IsTrigger = true;
            Assert.Throws<NotSupportedException>(() => _space.Update(0.02f));
            Assert.That(sensor.IsDisposed, Is.True);
            Assert.That(source.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator DisposeIsImmediateAndNativeDestructionIsDeferred()
        {
            var first = (BoxCollider2DUnity)_space.CreateBoxCollider(N.Vector2.One);
            var firstSource = first.Source;
            var disposed = 0;
            first.Disposed += () => disposed++;
            first.Dispose();
            Assert.That(first.IsDisposed, Is.True);
            Assert.That(firstSource != null, Is.True);
            Assert.That(firstSource.enabled, Is.False);
            Assert.That(disposed, Is.EqualTo(1));
            yield return null;
            Assert.That(firstSource == null, Is.True);
            var second = (BoxCollider2DUnity)_space.CreateBoxCollider(N.Vector2.One);
            U.Object.Destroy(second.Source);
            yield return null;
            Assert.That(second.IsDisposed, Is.False, "There is no per-frame validity observer.");
            _space.Update(0.02f);
            Assert.That(second.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => _ = second.Pose);
        }

        [UnityTest]
        public IEnumerator WorldDisposesChildrenBeforeItsEventAndUnloadsOwnedScene()
        {
            var collider = _space.CreateBoxCollider(N.Vector2.One);
            var scene = SceneOf(_space);
            var childEvents = 0;
            var worldEvents = 0;
            collider.Disposed += () => childEvents++;
            _space.Disposed += () =>
            {
                Assert.That(childEvents, Is.EqualTo(1));
                Assert.That(collider.IsDisposed, Is.True);
                worldEvents++;
            };
            _space.Dispose();
            _space.Dispose();
            Assert.That(worldEvents, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => _space.Update(0.02f));
            for (var frame = 0; frame < 100 && scene.IsValid() && scene.isLoaded; frame++) yield return null;
            Assert.That(scene.IsValid() && scene.isLoaded, Is.False);
            Assert.That(worldEvents, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OwnedRelayIsRemovedWithItsCollider()
        {
            var collider = (BoxCollider2DUnity)_space.CreateBoxCollider(N.Vector2.One);
            var host = collider.Source.gameObject;
            Assert.That(host.GetComponents<UnityCollisionRelay2D>().Length, Is.EqualTo(1));
            collider.Dispose();
            yield return null;
            Assert.That(host == null, Is.True);
        }

        [UnityTest]
        public IEnumerator ExternalSceneUnloadInvalidatesTheWorld()
        {
            var collider = _space.CreateBoxCollider(N.Vector2.One);
            var disposed = 0;
            _space.Disposed += () => disposed++;
            var unload = SceneManager.UnloadSceneAsync(SceneOf(_space));
            while (unload != null && !unload.isDone) yield return null;
            Assert.That(_space.IsValid, Is.False);
            Assert.That(collider.IsDisposed, Is.True);
            Assert.That(disposed, Is.EqualTo(1));
        }
    }
}
#endif
