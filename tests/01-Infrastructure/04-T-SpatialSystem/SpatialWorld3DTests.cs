using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Objects;
using BlackThunder.UniEngine.SpatialSystem;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture, Category("SpatialSystem")]
    public class SpatialWorld3DTests
    {
        [Test]
        public void WorldsOwnIndependentTimeAndExposedSettings()
        {
            using var first = new SpatialFixture3D();
            using var second = new SpatialFixture3D();
            first.Space.SetLayerCollision(2, 31, false);
            first.Space.Update(0.125f);
            first.Space.Update(0.25f);
            Assert.That(first.ElapsedTime, Is.EqualTo(0.375f));
            Assert.That(first.UpdateCount, Is.EqualTo(2));
            Assert.That(second.UpdateCount, Is.Zero);
            Assert.That(first.Space.GetLayerCollision(31, 2), Is.False);
            Assert.That(first.Space.GetLayerCollision(2, 31), Is.False);
            Assert.That(second.Space.GetLayerCollision(2, 31), Is.True);
            second.Space.Update(0.5f);
            Assert.That(second.ElapsedTime, Is.EqualTo(0.5f));
            Assert.That(first.ElapsedTime, Is.EqualTo(0.375f));
        }

        [Test]
        public void UpdateRejectsInvalidTimeAndNestedCallsAndRecoversAfterFailure()
        {
            using var fixture = new SpatialFixture3D();
            foreach (var value in new[] { 0f, -0.1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Space.Update(value));
            Assert.That(fixture.UpdateCount, Is.Zero);
            fixture.OnUpdate = _ => Assert.Throws<InvalidOperationException>(() => fixture.Space.Update(0.01f));
            fixture.Space.Update(0.1f);
            fixture.OnUpdate = _ => throw new SpatialEngineException("Native step failed.", new ApplicationException());
            Assert.Throws<SpatialEngineException>(() => fixture.Space.Update(0.1f));
            fixture.OnUpdate = null;
            fixture.Space.Update(0.1f);
            Assert.That(fixture.UpdateCount, Is.EqualTo(3));
            fixture.Space.Dispose();
            Assert.Throws<ObjectDisposedException>(() => fixture.Space.Update(0.1f));
        }

        [Test]
        public void FactoriesReturnOwnedHandlesWithWorldPoseAndLocalGeometry()
        {
            using var fixture = new SpatialFixture3D();
            var pose = new Transform3D(Vector3.One * 5f, Quaternion.Identity);
            var box = fixture.Space.CreateBoxCollider(Vector3.One * 2f, pose);
            var ball = fixture.Space.CreateSphereCollider(3f, pose);
            var capsule = fixture.Space.CreateCapsuleCollider(1f, 4f, CapsuleDirection3D.X, pose);
            Assert.That(box.Space, Is.SameAs(fixture.Space));
            Assert.That(ball.Space, Is.SameAs(fixture.Space));
            Assert.That(capsule.Space, Is.SameAs(fixture.Space));
            Assert.That(box.Size, Is.EqualTo(Vector3.One * 2f));
            Assert.That(ball.Radius, Is.EqualTo(3f));
            Assert.That(box.Pose.Position, Is.EqualTo(pose.Position));
            Assert.That(capsule.Pose.Position, Is.EqualTo(pose.Position));
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Space.CreateSphereCollider(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Space.SetLayerCollision(0, 32, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Space.GetLayerCollision(-1, 0));
            box.Dispose();
            Assert.That(box.IsDisposed, Is.True);
            Assert.That(ball.IsValid && capsule.IsValid, Is.True);
        }

        [Test]
        public void InvalidArgumentsAreNamedAfterTheParameterThatReceivedThem()
        {
            using var fixture = new SpatialFixture3D();
            var invalidPose = new Transform3D(new Vector3(float.NaN, 0f, 0f), Quaternion.Identity);
            var invalidDirection = (CapsuleDirection3D)9;
            var capsule = fixture.Space.CreateCapsuleCollider(1f, 2f);

            void Expect(string name, TestDelegate action) =>
                Assert.That(Assert.Throws<ArgumentOutOfRangeException>(action).ParamName, Is.EqualTo(name));

            Expect("pose", () => fixture.Space.CreateBoxCollider(Vector3.One, invalidPose));
            Expect("pose", () => fixture.Space.OverlapBox(invalidPose, Vector3.One));
            Expect("value", () => fixture.First.Pose = invalidPose);
            Expect("direction", () => fixture.Space.CreateCapsuleCollider(1f, 2f, invalidDirection));
            Expect("value", () => capsule.Direction = invalidDirection);
            Expect("direction", () => fixture.Space.RaycastAll(Vector3.Zero, Vector3.Zero));
            Expect("value", () => fixture.First.Layer = 32);
        }

        [Test]
        public void CreatedCollidersThatDoNotBelongToTheWorldAreReleased()
        {
            using var other = new SpatialFixture3D();
            var states = new List<SpatialFixture3D.ColliderState>();

            ColliderBindings3D Next()
            {
                var state = new SpatialFixture3D.ColliderState();
                states.Add(state);
                return other.Bindings(state);
            }

            SpatialProperty<T> Value<T>(T value) => new SpatialFixture3D.Cell<T>(value).Property;

            using var space = new CollisionSpace3D(new CollisionSpaceBindings3D(
                () => true,
                (origin, direction, distance, filter) => null,
                (pose, size, filter) => null,
                (center, radius, filter) => null,
                (first, second, radius, filter) => null,
                notify => () => { },
                new CollisionWorldBindings3D(
                    deltaTime => { },
                    (first, second) => true,
                    (first, second, value) => { },
                    (pose, size) => new BoxCollider3D(other.Space, Next(), Value(size), Value(Vector3.Zero)),
                    (pose, radius) => new SphereCollider3D(other.Space, Next(), Value(radius), Value(Vector3.Zero)),
                    (pose, radius, height, direction) => new CapsuleCollider3D(
                        other.Space, Next(), Value(radius), Value(height), Value(Vector3.Zero), Value(direction)))));

            Assert.Throws<InvalidOperationException>(() => space.CreateBoxCollider(Vector3.One));
            Assert.Throws<InvalidOperationException>(() => space.CreateSphereCollider(1f));
            Assert.Throws<InvalidOperationException>(() => space.CreateCapsuleCollider(1f, 2f));
            Assert.That(states.ConvertAll(state => state.Releases), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(other.Space.IsValid && other.First.IsValid && other.Second.IsValid, Is.True);
            Assert.That(space.IsValid, Is.True);
        }

        [Test]
        public void WorldDisposesFactoryChildrenBeforeNotifying()
        {
            using var fixture = new SpatialFixture3D();
            var ball = fixture.Space.CreateSphereCollider(1f);
            var box = fixture.Space.CreateBoxCollider(Vector3.One);
            var order = new List<string>();
            ((ICollisionComponent)ball).Disposed += () => order.Add("ball");
            ((ICollisionComponent)box).Disposed += () => order.Add("collider");
            fixture.Space.Disposed += () =>
            {
                Assert.That(ball.IsDisposed && box.IsDisposed, Is.True);
                order.Add("world");
            };
            fixture.Space.Dispose();
            Assert.That(order.Count, Is.EqualTo(3));
            Assert.That(order[2], Is.EqualTo("world"));
            fixture.Space.Dispose();
            Assert.That(order.Count, Is.EqualTo(3));
            Assert.That(fixture.Releases, Is.EqualTo(1));
        }
    }
}
