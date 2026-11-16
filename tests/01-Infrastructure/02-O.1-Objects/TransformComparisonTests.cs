using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Objects;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture]
    public class TransformComparisonTests
    {
        [Test]
        public void Transform2DUsesIndependentDistanceAndAngleTolerances()
        {
            var origin = Transform2D.Identity;
            var other = new Transform2D(new Vector2(0.25f, 0f), 0.125f);

            Assert.That(origin.Is(other, 0.25f, 0.125f), Is.True);
            Assert.That(other.Is(origin, 0.25f, 0.125f), Is.True);
            Assert.That(origin.Is(other, 0.2f, 0.125f), Is.False);
            Assert.That(origin.Is(other, 0.25f, 0.1f), Is.False);
            Assert.That(origin.Is(new Transform2D(new Vector2(0.75f, 0.75f), 0f), 1f, 0f), Is.False);
            Assert.That(origin.Is(origin, 0f, 0f), Is.True);
            Assert.That(origin.Is(new Transform2D(new Vector2(float.Epsilon, 0f), 0f), 0f, 0f), Is.False);
            Assert.That(origin.Is(new Transform2D(Vector2.Zero, float.Epsilon), 0f, 0f), Is.False);
        }

        [TestCase(1)]
        [TestCase(-1)]
        [TestCase(4)]
        public void Transform2DRecognizesWholeTurnsWithoutChangingStoredValues(int turns)
        {
            var origin = Transform2D.Identity;
            var other = new Transform2D(Vector2.Zero, turns * 360f);
            var original = other;

            Assert.That(origin.Is(other), Is.True);
            Assert.That(other.Is(origin), Is.True);
            Assert.That(origin.Is(other, 0f, 0f), Is.True);
            Assert.That(other, Is.EqualTo(original));
            Assert.That(origin.Equals(other), Is.False);
            Assert.That(origin == other, Is.False);
            Assert.That(new HashSet<Transform2D> { origin, other }.Count, Is.EqualTo(2));
        }

        [Test]
        public void Transform2DUsesTheShortestAngleAcrossTheWrapBoundary()
        {
            var before = new Transform2D(Vector2.Zero, 359.875f);
            var after = new Transform2D(Vector2.Zero, 0.125f);

            Assert.That(before.Is(after, 0f, 0.25f), Is.True);
            Assert.That(after.Is(before, 0f, 0.24f), Is.False);
            var half = new Transform2D(Vector2.Zero, 180f);
            Assert.That(Transform2D.Identity.Is(half), Is.False);
            Assert.That(Transform2D.Identity.Is(half, 0f, 180f), Is.True);
            Assert.That(Transform2D.Identity.Is(half, 0f, 179.99998f), Is.False);
        }

        [Test]
        public void Transform3DUsesIndependentDistanceAndAngleTolerances()
        {
            var origin = Transform3D.Identity;
            var other = new Transform3D(
                new Vector3(0f, 0f, 0.25f),
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(0.125d * Math.PI / 180d)));

            Assert.That(origin.Is(other, 0.25f, 0.126f), Is.True);
            Assert.That(other.Is(origin, 0.25f, 0.126f), Is.True);
            Assert.That(origin.Is(other, 0.2f, 0.126f), Is.False);
            Assert.That(origin.Is(other, 0.25f, 0.124f), Is.False);
            Assert.That(origin.Is(new Transform3D(new Vector3(0.75f), Quaternion.Identity), 1f, 0f), Is.False);
            Assert.That(origin.Is(origin, 0f, 0f), Is.True);
            Assert.That(
                origin.Is(new Transform3D(new Vector3(0f, 0f, float.Epsilon), Quaternion.Identity), 0f, 0f),
                Is.False);
        }

        [TestCase(-1f)]
        [TestCase(2f)]
        [TestCase(-2f)]
        public void Transform3DRecognizesEquivalentQuaternionRepresentations(float scale)
        {
            var origin = Transform3D.Identity;
            var other = new Transform3D(Vector3.Zero, new Quaternion(0f, 0f, 0f, scale));
            var original = other;

            Assert.That(origin.Is(other, 0f, 0f), Is.True);
            Assert.That(other.Is(origin, 0f, 0f), Is.True);
            Assert.That(other, Is.EqualTo(original));
            Assert.That(origin.Equals(other), Is.False);
            Assert.That(origin == other, Is.False);
            Assert.That(new HashSet<Transform3D> { origin, other }.Count, Is.EqualTo(2));
        }

        [Test]
        public void Transform3DComparesNonIdentityRotationsAndSmallAngularDifferences()
        {
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);
            var value = new Transform3D(new Vector3(1f, 2f, 3f), rotation);
            var oppositeSign = new Transform3D(value.Position, Quaternion.Negate(rotation));

            Assert.That(value.Is(oppositeSign), Is.True);
            var smallRotation = new Transform3D(
                Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(1e-6d * Math.PI / 180d)));
            Assert.That(Transform3D.Identity.Is(smallRotation), Is.True);
            Assert.That(Transform3D.Identity.Is(smallRotation, 0f, 1e-7f), Is.False);
            Assert.That(
                Transform3D.Identity.Is(new Transform3D(Vector3.Zero, new Quaternion(0f, 1f, 0f, 0f))),
                Is.False);
        }

        [TestCase(0.000005f, true)]
        [TestCase(0.00002f, false)]
        public void TransformDefaultAngularTolerancesUseDegrees(float angle, bool expected)
        {
            var value2D = new Transform2D(Vector2.Zero, angle);
            var value3D = new Transform3D(
                Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(angle * Math.PI / 180d)));

            Assert.That(Transform2D.Identity.Is(value2D), Is.EqualTo(expected));
            Assert.That(Transform3D.Identity.Is(value3D), Is.EqualTo(expected));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void BothTolerancesAreValidatedEvenWhenPositionsDiffer(float invalid)
        {
            var far2D = new Transform2D(new Vector2(100f), 0f);
            var far3D = new Transform3D(new Vector3(100f), Quaternion.Identity);

            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => Transform2D.Identity.Is(far2D, invalid)).ParamName,
                Is.EqualTo("positionTolerance"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => Transform3D.Identity.Is(far3D, invalid)).ParamName,
                Is.EqualTo("positionTolerance"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Transform2D.Identity.Is(far2D, angularTolerance: invalid)).ParamName,
                Is.EqualTo("angularTolerance"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Transform3D.Identity.Is(far3D, angularTolerance: invalid)).ParamName,
                Is.EqualTo("angularTolerance"));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonfinitePositionsDoNotMatch(float invalid)
        {
            var invalid2D = new Transform2D(new Vector2(invalid, 0f), 0f);
            var invalid3D = new Transform3D(new Vector3(0f, 0f, invalid), Quaternion.Identity);

            Assert.That(invalid2D.Is(Transform2D.Identity), Is.False);
            Assert.That(Transform2D.Identity.Is(invalid2D), Is.False);
            Assert.That(invalid3D.Is(Transform3D.Identity), Is.False);
            Assert.That(Transform3D.Identity.Is(invalid3D), Is.False);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonfiniteRotationsAreRejectedEvenWhenPositionsDiffer(float invalid)
        {
            var invalid2D = new Transform2D(new Vector2(100f), invalid);
            var invalid3D = new Transform3D(new Vector3(100f), new Quaternion(invalid, 0f, 0f, 1f));

            Assert.Throws<ArgumentException>(() => invalid2D.Is(Transform2D.Identity));
            Assert.Throws<ArgumentException>(() => Transform2D.Identity.Is(invalid2D));
            Assert.Throws<ArgumentException>(() => invalid3D.Is(Transform3D.Identity));
            Assert.Throws<ArgumentException>(() => Transform3D.Identity.Is(invalid3D));
        }

        [Test]
        public void ZeroQuaternionHasNoComparableRotation()
        {
            Assert.Throws<ArgumentException>(() => default(Transform3D).Is(Transform3D.Identity));
            Assert.Throws<ArgumentException>(() => Transform3D.Identity.Is(default));
            Assert.That(default(Transform3D).Equals(default(Transform3D)), Is.True);
        }

        [Test]
        public void LargeFiniteValuesDoNotOverflowDuringComparison()
        {
            var large2D = new Transform2D(new Vector2(float.MaxValue, 0f), float.MaxValue);
            var negative2D = new Transform2D(new Vector2(-float.MaxValue, 0f), -float.MaxValue);
            var large3D = new Transform3D(new Vector3(float.MaxValue, 0f, 0f), Quaternion.Identity);
            var negative3D = new Transform3D(new Vector3(-float.MaxValue, 0f, 0f), Quaternion.Identity);

            Assert.That(large2D.Is(large2D, 0f, 0f), Is.True);
            Assert.That(large3D.Is(large3D, 0f, 0f), Is.True);
            Assert.That(large2D.Is(negative2D, float.MaxValue, 180f), Is.False);
            Assert.That(large3D.Is(negative3D, float.MaxValue, 0f), Is.False);
            Assert.That(
                new Transform2D(Vector2.Zero, float.MaxValue).Is(
                    new Transform2D(Vector2.Zero, -float.MaxValue), 0f, 180f),
                Is.True);
        }
    }
}
