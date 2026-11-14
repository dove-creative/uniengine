using System;
using System.Numerics;
using Blackthunder.UniEngine.Mathematics;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture]
    [Category("Math")]
    public class UniMathTests
    {
        [Test]
        public void ScalarEqualityIncludesBoundaryAndSupportsZeroTolerance()
        {
            Assert.That(1f.Is(1.125f, 0.125f), Is.True);
            Assert.That(1f.Is(1.25f, 0.125f), Is.False);
            Assert.That(1f.Is(1f, 0f), Is.True);
            Assert.That((-0f).IsZero(0f), Is.True);
            Assert.That(UniMath.Epsilon.IsZero(), Is.True);
            Assert.That(float.Epsilon.IsZero(0f), Is.False);
            Assert.That(float.MaxValue.Is(-float.MaxValue, float.MaxValue), Is.False);
        }

        [TestCase(-0.25f, false, true, false, true)]
        [TestCase(-0.125f, false, false, false, true)]
        [TestCase(0f, false, false, true, true)]
        [TestCase(0.125f, false, false, true, false)]
        [TestCase(0.25f, true, false, true, false)]
        public void SignChecksPreserveStrictAndTolerantBoundaries(
            float value, bool positive, bool negative, bool tolerantPositive, bool tolerantNegative)
        {
            Assert.That(value.IsPositive(false, 0.125f), Is.EqualTo(positive));
            Assert.That(value.IsNegative(false, 0.125f), Is.EqualTo(negative));
            Assert.That(value.IsPositive(true, 0.125f), Is.EqualTo(tolerantPositive));
            Assert.That(value.IsNegative(true, 0.125f), Is.EqualTo(tolerantNegative));
        }

        [TestCase(0.75f, false, true, false, true)]
        [TestCase(0.875f, false, true, true, true)]
        [TestCase(1f, false, false, true, true)]
        [TestCase(1.125f, true, false, true, true)]
        [TestCase(1.25f, true, false, true, false)]
        public void OrderingPreservesInclusiveShiftedBoundaries(
            float value, bool more, bool less, bool tolerantMore, bool tolerantLess)
        {
            Assert.That(value.IsMoreThan(1f, false, 0.125f), Is.EqualTo(more));
            Assert.That(value.IsLessThan(1f, false, 0.125f), Is.EqualTo(less));
            Assert.That(value.IsMoreThan(1f, true, 0.125f), Is.EqualTo(tolerantMore));
            Assert.That(value.IsLessThan(1f, true, 0.125f), Is.EqualTo(tolerantLess));
            Assert.That(1f.IsMoreThan(1f, false, 0f), Is.True);
            Assert.That(1f.IsLessThan(1f, false, 0f), Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonfiniteComparisonOperandsNeverMatch(float value)
        {
            Assert.That(value.Is(value), Is.False);
            Assert.That(value.Is(0f), Is.False);
            Assert.That(0f.Is(value), Is.False);
            Assert.That(value.IsZero(), Is.False);
            foreach (var tolerant in new[] { false, true })
            {
                Assert.That(value.IsPositive(tolerant), Is.False);
                Assert.That(value.IsNegative(tolerant), Is.False);
                Assert.That(value.IsMoreThan(0f, tolerant), Is.False);
                Assert.That(value.IsLessThan(0f, tolerant), Is.False);
                Assert.That(0f.IsMoreThan(value, tolerant), Is.False);
                Assert.That(0f.IsLessThan(value, tolerant), Is.False);
            }

            var v2 = new Vector2(0f, value);
            var v3 = new Vector3(0f, 0f, value);
            var v4 = new Vector4(0f, 0f, 0f, value);
            Assert.That(v2.Is(v2), Is.False);
            Assert.That(v3.Is(v3), Is.False);
            Assert.That(v4.Is(v4), Is.False);
            Assert.That(Vector2.Zero.Is(v2), Is.False);
            Assert.That(Vector3.Zero.Is(v3), Is.False);
            Assert.That(Vector4.Zero.Is(v4), Is.False);
            Assert.That(v2.IsZero(), Is.False);
            Assert.That(v3.IsZero(), Is.False);
            Assert.That(v4.IsZero(), Is.False);
            Assert.That(value.SnapTo(0f, out var snapped), Is.EqualTo(value));
            Assert.That(snapped, Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void AllComparisonEntryPointsRejectInvalidTolerance(float tolerance)
        {
            var checks = new TestDelegate[]
            {
                () => 0f.Is(0f, tolerance),
                () => 0f.IsZero(tolerance),
                () => 0f.IsPositive(false, tolerance),
                () => 0f.IsNegative(true, tolerance),
                () => 0f.IsMoreThan(0f, false, tolerance),
                () => 0f.IsLessThan(0f, true, tolerance),
                () => 0f.SnapTo(0f, tolerance, out _),
                () => Vector2.Zero.Is(Vector2.Zero, tolerance),
                () => Vector3.Zero.Is(Vector3.Zero, tolerance),
                () => Vector4.Zero.Is(Vector4.Zero, tolerance),
                () => Vector2.Zero.IsZero(tolerance),
                () => Vector3.Zero.IsZero(tolerance),
                () => Vector4.Zero.IsZero(tolerance),
                () => Vector2.UnitX.IsParallelWith(Vector2.UnitX, out _, tolerance)
            };
            foreach (var check in checks)
                Assert.That(Assert.Throws<ArgumentOutOfRangeException>(check).ParamName, Is.EqualTo("epsilon"));

            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(
                () => Quaternion.Identity.IsSameRotationAs(Quaternion.Identity, tolerance)).ParamName,
                Is.EqualTo("angularTolerance"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(
                () => Quaternion.Identity.IsHalfTurnFrom(Quaternion.Identity, tolerance)).ParamName,
                Is.EqualTo("angularTolerance"));
        }

        [Test]
        public void VectorEqualityUsesEuclideanDistanceAndIncludesBoundary()
        {
            Assert.That(new Vector2(3f, 4f).IsZero(5f), Is.True);
            Assert.That(new Vector3(0f, 3f, 4f).IsZero(5f), Is.True);
            Assert.That(new Vector4(0f, 0f, 3f, 4f).IsZero(5f), Is.True);
            Assert.That(new Vector2(1f, 1f).IsZero(1f), Is.False);
            Assert.That(new Vector3(1f, 1f, 1f).IsZero(1f), Is.False);
            Assert.That(new Vector4(1f, 1f, 1f, 1f).IsZero(1f), Is.False);
            Assert.That(Vector2.One.Is(Vector2.One, 0f), Is.True);
            Assert.That(Vector3.One.Is(Vector3.One, 0f), Is.True);
            Assert.That(Vector4.One.Is(Vector4.One, 0f), Is.True);
            Assert.That(Vector2.Zero.IsZero(0f), Is.True);
            Assert.That(Vector3.Zero.IsZero(0f), Is.True);
            Assert.That(Vector4.Zero.IsZero(), Is.True);
            Assert.That(Vector4.Zero.IsZero(0f), Is.True);
            Assert.That(new Vector4(0f, 0f, 0f, UniMath.Epsilon).IsZero(), Is.True);
            Assert.That(new Vector4(0f, 0f, 0f, UniMath.Epsilon * 2f).IsZero(), Is.False);
        }

        [Test]
        public void VectorDistancesAvoidFloatOverflowAndUnderflow()
        {
            Assert.That(new Vector2(float.MaxValue, 0f).IsZero(float.MaxValue), Is.True);
            Assert.That(new Vector3(float.MaxValue, float.MaxValue, 0f).IsZero(float.MaxValue), Is.False);
            Assert.That(new Vector4(float.MaxValue).IsZero(float.MaxValue), Is.False);
            Assert.That(new Vector2(float.MaxValue, 0f).Is(new Vector2(-float.MaxValue, 0f), float.MaxValue),
                Is.False);
            Assert.That(new Vector2(float.Epsilon, 0f).IsZero(0f), Is.False);
            Assert.That(new Vector3(0f, float.Epsilon, 0f).IsZero(0f), Is.False);
            Assert.That(new Vector4(0f, 0f, 0f, float.Epsilon).IsZero(0f), Is.False);
            Assert.That(new Vector4(0f, 0f, 0f, float.Epsilon).IsZero(float.Epsilon), Is.True);
        }

        [TestCase(5.7f, 1f, 6f)]
        [TestCase(-5.7f, 1f, -6f)]
        [TestCase(0.5f, 1f, 0f)]
        [TestCase(1.5f, 1f, 2f)]
        [TestCase(2.5f, 1f, 2f)]
        [TestCase(-1.5f, 1f, -2f)]
        [TestCase(-2.5f, 1f, -2f)]
        [TestCase(1.25f, 0.5f, 1f)]
        public void SnapRoundsMidpointsToEven(float value, float unit, float expected)
        {
            Assert.That(value.Snap(unit), Is.EqualTo(expected));
        }

        [Test]
        public void SnapSupportsSmallUnitsAndEachVectorComponent()
        {
            var unit = 1f / 1048576f;
            Assert.That((2.5f * unit).Snap(unit), Is.EqualTo(2f * unit));
            Assert.That(float.Epsilon.Snap(float.Epsilon), Is.EqualTo(float.Epsilon));
            Assert.That(float.MaxValue.Snap(float.Epsilon), Is.EqualTo(float.MaxValue));
            Assert.That(new Vector2(2.5f, -3.5f).Snap(1f), Is.EqualTo(new Vector2(2f, -4f)));
            Assert.That(new Vector2(2.5f, -3.5f).Snap(new Vector2(2f, 3f)),
                Is.EqualTo(new Vector2(2f, -3f)));
            Assert.That(new Vector3(2.5f, -3.5f, 4.5f).Snap(1f), Is.EqualTo(new Vector3(2f, -4f, 4f)));
            Assert.That(new Vector3(2.5f, -3.5f, 4.5f).Snap(new Vector3(2f, 3f, 2f)),
                Is.EqualTo(new Vector3(2f, -3f, 4f)));
            Assert.That(float.NaN.Snap(1f), Is.NaN);
            Assert.That(float.PositiveInfinity.Snap(1f), Is.EqualTo(float.PositiveInfinity));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void SnapRejectsInvalidUnitsIncludingVectorComponents(float unit)
        {
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => 1f.Snap(unit)).ParamName,
                Is.EqualTo("unit"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Vector2.One.Snap(unit));
            Assert.Throws<ArgumentOutOfRangeException>(() => Vector3.One.Snap(unit));
            Assert.Throws<ArgumentOutOfRangeException>(() => Vector2.One.Snap(new Vector2(1f, unit)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Vector3.One.Snap(new Vector3(1f, 1f, unit)));
        }

        [Test]
        public void SnapToReturnsCriterionOnlyWithinInclusiveTolerance()
        {
            Assert.That(1.125f.SnapTo(1f, 0.125f, out var snapped), Is.EqualTo(1f));
            Assert.That(snapped, Is.True);
            Assert.That(1.25f.SnapTo(1f, 0.125f, out snapped), Is.EqualTo(1.25f));
            Assert.That(snapped, Is.False);
            Assert.That(0f.SnapTo(UniMath.Epsilon, out snapped), Is.EqualTo(UniMath.Epsilon));
            Assert.That(snapped, Is.True);
            Assert.That(1f.SnapTo(float.PositiveInfinity, out snapped), Is.EqualTo(1f));
            Assert.That(snapped, Is.False);
        }

        [TestCase(3f, 4f, 6f, 8f, true, true)]
        [TestCase(3f, 4f, -6f, -8f, true, false)]
        [TestCase(1f, 0f, 0f, 1f, false, false)]
        [TestCase(1f, 0f, 1f, 0.001f, true, true)]
        [TestCase(1f, 0f, 1f, 0.01f, false, false)]
        [TestCase(float.MaxValue, 0f, float.Epsilon, 0f, true, true)]
        [TestCase(float.MaxValue, float.MaxValue, -float.Epsilon, -float.Epsilon, true, false)]
        public void ParallelChecksUseDirectionIndependentOfMagnitude(
            float ax, float ay, float bx, float by, bool parallel, bool sameDirection)
        {
            Assert.That(new Vector2(ax, ay).IsParallelWith(new Vector2(bx, by), out var same),
                Is.EqualTo(parallel));
            Assert.That(same, Is.EqualTo(sameDirection));
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void DirectionComparisonsRejectInvalidOperands(float component)
        {
            var vector = new Vector2(component, 0f);
            Assert.That(Assert.Throws<ArgumentException>(() => vector.IsParallelWith(Vector2.UnitX, out _)).ParamName,
                Is.EqualTo("value"));
            Assert.That(Assert.Throws<ArgumentException>(() => Vector2.UnitX.IsParallelWith(vector, out _)).ParamName,
                Is.EqualTo("other"));
            var rotation = new Quaternion(0f, 0f, 0f, component);
            Assert.Throws<ArgumentException>(() => rotation.IsSameRotationAs(Quaternion.Identity));
            Assert.Throws<ArgumentException>(() => Quaternion.Identity.IsSameRotationAs(rotation));
            Assert.Throws<ArgumentException>(() => rotation.IsHalfTurnFrom(Quaternion.Identity));
            Assert.Throws<ArgumentException>(() => Quaternion.Identity.IsHalfTurnFrom(rotation));
        }

        [Test]
        public void RotationEqualityAccountsForSignScaleAndExactZeroTolerance()
        {
            var rotation = new Quaternion(1f, 2f, 3f, 4f);
            Assert.That(rotation.IsSameRotationAs(rotation, 0f), Is.True);
            Assert.That(rotation.IsSameRotationAs(Quaternion.Negate(rotation), 0f), Is.True);
            Assert.That(rotation.IsSameRotationAs(Quaternion.Multiply(rotation, 2f), 0f), Is.True);
            Assert.That(new Quaternion(0f, 0f, 0f, float.MaxValue)
                .IsSameRotationAs(new Quaternion(0f, 0f, 0f, -float.Epsilon), 0f), Is.True);
            Assert.That(rotation.IsHalfTurnFrom(rotation), Is.False);
        }

        [Test]
        public void RotationComparisonsDistinguishSameQuarterAndHalfTurns()
        {
            var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(System.Math.PI / 2d));
            var half = new Quaternion(0f, 1f, 0f, 0f);
            Assert.That(Quaternion.Identity.IsSameRotationAs(quarter), Is.False);
            Assert.That(Quaternion.Identity.IsHalfTurnFrom(quarter), Is.False);
            Assert.That(Quaternion.Identity.IsSameRotationAs(half), Is.False);
            Assert.That(Quaternion.Identity.IsHalfTurnFrom(half, 0f), Is.True);
            Assert.That(Quaternion.Identity.IsHalfTurnFrom(Quaternion.Negate(half), 0f), Is.True);
            Assert.That(quarter.IsHalfTurnFrom(Quaternion.Conjugate(quarter)), Is.True);
        }

        [TestCase(0.000001f)]
        [TestCase(0.00001f)]
        [TestCase(0.25f)]
        public void AngularToleranceUsesRadiansAndRetainsSmallDifferences(float angle)
        {
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle);
            Assert.That(Quaternion.Identity.IsSameRotationAs(rotation, angle * 2f), Is.True);
            Assert.That(Quaternion.Identity.IsSameRotationAs(rotation, angle * 0.5f), Is.False);
            var nearHalf = new Quaternion(0f, 0f,
                (float)System.Math.Cos(angle / 2d), (float)System.Math.Sin(angle / 2d));
            Assert.That(Quaternion.Identity.IsHalfTurnFrom(nearHalf, angle * 2f), Is.True);
            Assert.That(Quaternion.Identity.IsHalfTurnFrom(nearHalf, angle * 0.5f), Is.False);
        }

        [Test]
        public void RotationToleranceHandlesPiBoundary()
        {
            // A half turn is just below the nearest float representation of pi.
            var half = new Quaternion(1f, 0f, 0f, 0f);
            Assert.That(Quaternion.Identity.IsSameRotationAs(half, (float)System.Math.PI), Is.True);
            Assert.That(Quaternion.Identity.IsSameRotationAs(half, 3.1415925f), Is.False);
            Assert.That(Quaternion.Identity.IsHalfTurnFrom(Quaternion.Identity, (float)System.Math.PI), Is.True);
            Assert.That(Quaternion.Identity.IsHalfTurnFrom(Quaternion.Identity, 3.1415925f), Is.False);
        }
    }
}
