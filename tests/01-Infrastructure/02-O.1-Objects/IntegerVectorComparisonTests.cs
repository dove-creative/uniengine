using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Objects;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture]
    public class IntegerVectorComparisonTests
    {
        [Test]
        public void DefaultTolerancePreservesExactIntegerCoordinates()
        {
            var value2D = new Vector2Int(int.MaxValue, int.MinValue);
            var value3D = new Vector3Int(int.MaxValue, int.MinValue, int.MaxValue);

            Assert.That(value2D.Is(value2D), Is.True);
            Assert.That(value3D.Is(value3D), Is.True);
            Assert.That(value2D.Is(value2D, 0f), Is.True);
            Assert.That(value3D.Is(value3D, 0f), Is.True);
            Assert.That(value2D.Is(new Vector2Int(int.MaxValue - 1, int.MinValue)), Is.False);
            Assert.That(value2D.Is(new Vector2Int(int.MaxValue, int.MinValue + 1)), Is.False);
            Assert.That(value3D.Is(new Vector3Int(int.MaxValue - 1, int.MinValue, int.MaxValue)), Is.False);
            Assert.That(value3D.Is(new Vector3Int(int.MaxValue, int.MinValue + 1, int.MaxValue)), Is.False);
            Assert.That(value3D.Is(new Vector3Int(int.MaxValue, int.MinValue, int.MaxValue - 1)), Is.False);
        }

        [Test]
        public void DistanceToleranceIncludesBoundaryWithoutChangingExactEquality()
        {
            var value2D = new Vector2Int(3, 4);
            var value3D = new Vector3Int(3, 4, 12);

            Assert.That(Vector2Int.Zero.Is(value2D, 5f), Is.True);
            Assert.That(value2D.Is(Vector2Int.Zero, 5f), Is.True);
            Assert.That(Vector2Int.Zero.Is(value2D, 4.99f), Is.False);
            Assert.That(Vector2Int.Zero.Is(new Vector2Int(1, 1), 1f), Is.False);
            Assert.That(Vector3Int.Zero.Is(value3D, 13f), Is.True);
            Assert.That(value3D.Is(Vector3Int.Zero, 13f), Is.True);
            Assert.That(Vector3Int.Zero.Is(value3D, 12.99f), Is.False);
            Assert.That(Vector3Int.Zero.Is(new Vector3Int(1, 1, 1), 1f), Is.False);
            Assert.That(value2D == Vector2Int.Zero, Is.False);
            Assert.That(value3D == Vector3Int.Zero, Is.False);
            Assert.That(new HashSet<Vector2Int> { Vector2Int.Zero, value2D }.Count, Is.EqualTo(2));
            Assert.That(new HashSet<Vector3Int> { Vector3Int.Zero, value3D }.Count, Is.EqualTo(2));
        }

        [Test]
        public void ExtremeCoordinateDifferencesDoNotOverflowOrLoseAdjacentIntegers()
        {
            var minimum2D = new Vector2Int(int.MinValue, 0);
            var maximum2D = new Vector2Int(int.MaxValue, 0);
            var minimum3D = new Vector3Int(0, 0, int.MinValue);
            var maximum3D = new Vector3Int(0, 0, int.MaxValue);

            Assert.That(minimum2D.Is(maximum2D, int.MaxValue), Is.False);
            Assert.That(minimum3D.Is(maximum3D, int.MaxValue), Is.False);
            Assert.That(minimum2D.Is(maximum2D, 4294967296f), Is.True);
            Assert.That(minimum3D.Is(maximum3D, 4294967296f), Is.True);
            Assert.That(maximum2D.Is(new Vector2Int(int.MaxValue - 1, 0), 0.5f), Is.False);
            Assert.That(maximum3D.Is(new Vector3Int(0, 0, int.MaxValue - 1), 0.5f), Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDistanceToleranceIsRejectedEvenForIdenticalValues(float invalid)
        {
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Vector2Int.Zero.Is(Vector2Int.Zero, invalid)).ParamName,
                Is.EqualTo("tolerance"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Vector3Int.Zero.Is(Vector3Int.Zero, invalid)).ParamName,
                Is.EqualTo("tolerance"));
        }
    }
}
