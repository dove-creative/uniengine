using System.Numerics;
using BlackThunder.UniEngine.Mathematics;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture]
    [Category("Math")]
    public class UniMathVectorTests
    {
        [TestCase(0f, true)]
        [TestCase(float.MaxValue, true)]
        [TestCase(float.Epsilon, true)]
        [TestCase(float.NaN, false)]
        [TestCase(float.PositiveInfinity, false)]
        [TestCase(float.NegativeInfinity, false)]
        public void FiniteChecksInspectEveryComponent(float value, bool expected)
        {
            Assert.That(UniMath.IsFinite(new Vector2(value, 0f)), Is.EqualTo(expected));
            Assert.That(UniMath.IsFinite(new Vector2(0f, value)), Is.EqualTo(expected));
            Assert.That(UniMath.IsFinite(new Vector3(value, 0f, 0f)), Is.EqualTo(expected));
            Assert.That(UniMath.IsFinite(new Vector3(0f, value, 0f)), Is.EqualTo(expected));
            Assert.That(UniMath.IsFinite(new Vector3(0f, 0f, value)), Is.EqualTo(expected));
            Assert.That(UniMath.IsFinite(new Vector4(value, 0f, 0f, 0f)), Is.EqualTo(expected));
            Assert.That(UniMath.IsFinite(new Vector4(0f, value, 0f, 0f)), Is.EqualTo(expected));
            Assert.That(UniMath.IsFinite(new Vector4(0f, 0f, value, 0f)), Is.EqualTo(expected));
            Assert.That(UniMath.IsFinite(new Vector4(0f, 0f, 0f, value)), Is.EqualTo(expected));
        }

        [Test]
        public void DistancePreservesLargeAndTinyResultsInDouble()
        {
            Assert.That(UniMath.Distance(new Vector2(1f, 2f), new Vector2(4f, 6f)), Is.EqualTo(5d));
            Assert.That(UniMath.Distance(Vector2.One, Vector2.One), Is.Zero);
            var left = new Vector2(-float.MaxValue, 0f);
            var right = new Vector2(float.MaxValue, 0f);
            Assert.That(UniMath.Distance(left, right), Is.EqualTo(2d * float.MaxValue));
            Assert.That(UniMath.Distance(right, left), Is.EqualTo(2d * float.MaxValue));
            Assert.That(UniMath.Distance(Vector2.Zero, new Vector2(float.Epsilon, 0f)),
                Is.EqualTo((double)float.Epsilon));
        }

        [TestCase(-1d, -3f, -4f)]
        [TestCase(0d, 1f, 2f)]
        [TestCase(0.5d, 3f, 5f)]
        [TestCase(1d, 5f, 8f)]
        [TestCase(2d, 9f, 14f)]
        public void LerpPreservesEndpointsAndAllowsExtrapolation(double rate, float x, float y)
        {
            Assert.That(UniMath.Lerp(new Vector2(1f, 2f), new Vector2(5f, 8f), rate),
                Is.EqualTo(new Vector2(x, y)));
        }

        [Test]
        public void LerpRetainsDoubleRateAndAvoidsIntermediateFloatOverflow()
        {
            Assert.That(UniMath.Lerp(new Vector2(-float.MaxValue), new Vector2(float.MaxValue), 0.5d),
                Is.EqualTo(Vector2.Zero));
            var precise = UniMath.Lerp(new Vector2(-100000000f), new Vector2(100000000f),
                0.5d + 1d / 1073741824d);
            Assert.That(precise.X, Is.EqualTo(0.18626451f).Within(0.000001f));
            Assert.That(precise.Y, Is.EqualTo(precise.X));
        }

        [TestCase(1f, 0f, 0f)]
        [TestCase(0f, 1f, 90f)]
        [TestCase(-1f, 0f, 180f)]
        [TestCase(0f, -1f, -90f)]
        [TestCase(1f, 1f, 45f)]
        [TestCase(0f, 0f, 0f)]
        public void RotationUsesDegreesFromPositiveX(float x, float y, float expected)
        {
            var from = new Vector2(2f, 3f);
            Assert.That(UniMath.Rotation(from, from + new Vector2(x, y)), Is.EqualTo(expected));
        }

        [Test]
        public void RotationHandlesExtremeCoordinatesBeforeSubtraction()
        {
            Assert.That(UniMath.Rotation(new Vector2(-float.MaxValue), new Vector2(float.MaxValue)),
                Is.EqualTo(45f));
        }

        [Test]
        public void CalculationsPreserveFloatingPointNonfiniteResults()
        {
            Assert.That(UniMath.Distance(Vector2.Zero, new Vector2(float.PositiveInfinity, 0f)),
                Is.EqualTo(double.PositiveInfinity));
            Assert.That(double.IsNaN(UniMath.Distance(Vector2.Zero, new Vector2(float.NaN, 0f))), Is.True);
            Assert.That(float.IsNaN(UniMath.Lerp(Vector2.Zero, Vector2.One, double.NaN).X), Is.True);
            Assert.That(float.IsNaN(UniMath.Rotation(Vector2.Zero, new Vector2(float.NaN, 0f))), Is.True);
        }
    }
}
