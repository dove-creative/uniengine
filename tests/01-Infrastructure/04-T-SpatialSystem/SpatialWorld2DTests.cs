using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Objects;
using BlackThunder.UniEngine.SpatialSystem;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture, Category("SpatialSystem")]
    public class SpatialWorld2DTests
    {
        [Test]
        public void WorldsOwnIndependentTimeAndExposedSettings()
        {
            using var first = new SpatialFixture2D();
            using var second = new SpatialFixture2D();
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
            using var fixture = new SpatialFixture2D();
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
            using var fixture = new SpatialFixture2D();
            var pose = new Transform2D(Vector2.One * 5f, 30f);
            var box = fixture.Space.CreateBoxCollider(Vector2.One * 2f, pose);
            var ball = fixture.Space.CreateCircleCollider(3f, pose);
            var capsule = fixture.Space.CreateCapsuleCollider(new Vector2(2f, 4f), CapsuleDirection2D.Horizontal, pose);
            Assert.That(box.Space, Is.SameAs(fixture.Space));
            Assert.That(ball.Space, Is.SameAs(fixture.Space));
            Assert.That(capsule.Space, Is.SameAs(fixture.Space));
            Assert.That(box.Size, Is.EqualTo(Vector2.One * 2f));
            Assert.That(ball.Radius, Is.EqualTo(3f));
            Assert.That(box.Pose.Position, Is.EqualTo(pose.Position));
            Assert.That(capsule.Pose.Position, Is.EqualTo(pose.Position));
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Space.CreateCircleCollider(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Space.SetLayerCollision(0, 32, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Space.GetLayerCollision(-1, 0));
            box.Dispose();
            Assert.That(box.IsDisposed, Is.True);
            Assert.That(ball.IsValid && capsule.IsValid, Is.True);
        }

        [Test]
        public void InvalidArgumentsAreNamedAfterTheParameterThatReceivedThem()
        {
            using var fixture = new SpatialFixture2D();
            var invalidPose = new Transform2D(new Vector2(float.NaN, 0f), 0f);
            var invalidDirection = (CapsuleDirection2D)9;
            var capsule = fixture.Space.CreateCapsuleCollider(Vector2.One);

            void Expect(string name, TestDelegate action) =>
                Assert.That(Assert.Throws<ArgumentOutOfRangeException>(action).ParamName, Is.EqualTo(name));

            Expect("pose", () => fixture.Space.CreateBoxCollider(Vector2.One, invalidPose));
            Expect("pose", () => fixture.Space.OverlapBox(invalidPose, Vector2.One));
            Expect("pose", () => fixture.Space.OverlapCapsule(
                invalidPose, Vector2.One, CapsuleDirection2D.Vertical));
            Expect("direction", () => fixture.Space.OverlapCapsule(
                new Transform2D(Vector2.Zero, 0f), Vector2.One, invalidDirection));
            Expect("value", () => fixture.First.Pose = invalidPose);
            Expect("direction", () => fixture.Space.CreateCapsuleCollider(Vector2.One, invalidDirection));
            Expect("value", () => capsule.Direction = invalidDirection);
            Expect("direction", () => fixture.Space.RaycastAll(Vector2.Zero, Vector2.Zero));
            Expect("value", () => fixture.First.Layer = 32);
        }

        [Test]
        public void CreatedCollidersThatDoNotBelongToTheWorldAreReleased()
        {
            using var other = new SpatialFixture2D();
            var states = new List<SpatialFixture2D.ColliderState>();

            ColliderBindings2D Next()
            {
                var state = new SpatialFixture2D.ColliderState();
                states.Add(state);
                return other.Bindings(state);
            }

            SpatialProperty<T> Value<T>(T value) => new SpatialFixture2D.Cell<T>(value).Property;

            using var space = new CollisionSpace2D(new CollisionSpaceBindings2D(
                () => true,
                (origin, direction, distance, filter) => null,
                (pose, size, filter) => null,
                (center, radius, filter) => null,
                (pose, size, direction, filter) => null,
                notify => () => { },
                new CollisionWorldBindings2D(
                    deltaTime => { },
                    (first, second) => true,
                    (first, second, value) => { },
                    (pose, size) => new BoxCollider2D(other.Space, Next(), Value(size), Value(Vector2.Zero)),
                    (pose, radius) => new CircleCollider2D(other.Space, Next(), Value(radius), Value(Vector2.Zero)),
                    (pose, size, direction) => new CapsuleCollider2D(
                        other.Space, Next(), Value(size), Value(Vector2.Zero), Value(direction)))));

            Assert.Throws<InvalidOperationException>(() => space.CreateBoxCollider(Vector2.One));
            Assert.Throws<InvalidOperationException>(() => space.CreateCircleCollider(1f));
            Assert.Throws<InvalidOperationException>(() => space.CreateCapsuleCollider(Vector2.One));
            Assert.That(states.ConvertAll(state => state.Releases), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(other.Space.IsValid && other.First.IsValid && other.Second.IsValid, Is.True);
            Assert.That(space.IsValid, Is.True);
        }

        [Test]
        public void WorldDisposesFactoryChildrenBeforeNotifying()
        {
            using var fixture = new SpatialFixture2D();
            var ball = fixture.Space.CreateCircleCollider(1f);
            var box = fixture.Space.CreateBoxCollider(Vector2.One);
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
