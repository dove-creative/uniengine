using System;
using System.Numerics;
using BlackThunder.UniEngine.Mathematics;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture]
    [Category("VectorFunctions")]
    public class VectorFunctionTests
    {
        [TestCase(0f, 1f, 2f)]
        [TestCase(0.5f, 3f, 5f)]
        [TestCase(1f, 5f, 8f)]
        [TestCase(-1f, -3f, -4f)]
        [TestCase(2f, 9f, 14f)]
        public void LineEvaluatesParameterIncludingExtrapolation(float t, float x, float y)
        {
            IVectorFunction line = new AffineLine(new Vector2(1f, 2f), new Vector2(4f, 6f));
            Assert.That(line.Evaluate(t), Is.EqualTo(new Vector2(x, y)));
        }

        [Test]
        public void LinePropertiesAndNormalizationControlSubsequentEvaluation()
        {
            var line = new AffineLine();
            Assert.That(line.Evaluate(3f), Is.EqualTo(Vector2.Zero));
            line.Origin = new Vector2(1f, 2f);
            line.DirectionVector = new Vector2(3f, 4f);
            Assert.That(line.Evaluate(1f), Is.EqualTo(new Vector2(4f, 6f)));
            line.Normalize();
            Assert.That(line.DirectionVector, Is.EqualTo(new Vector2(0.6f, 0.8f)));
            Assert.That(line.Origin, Is.EqualTo(new Vector2(1f, 2f)));
            var normalized = new AffineLine(line.Origin, new Vector2(3f, 4f), true);
            Assert.That(normalized.DirectionVector, Is.EqualTo(line.DirectionVector));
            Assert.That(normalized.Evaluate(2f), Is.EqualTo(line.Evaluate(2f)));
        }

        [TestCase(float.Epsilon)]
        [TestCase(float.MaxValue)]
        [TestCase(-float.MaxValue)]
        public void LineNormalizesEveryFiniteNonzeroMagnitude(float component)
        {
            var line = new AffineLine(Vector2.One, new Vector2(component, 0f), true);
            Assert.That(line.DirectionVector, Is.EqualTo(new Vector2(component > 0f ? 1f : -1f, 0f)));
            line.DirectionVector = new Vector2(component, component);
            line.Normalize();
            Assert.That(line.DirectionVector.Length(), Is.EqualTo(1f).Within(0.000001f));
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidNormalizationPreservesState(float component)
        {
            var direction = new Vector2(component, 0f);
            var line = new AffineLine(Vector2.One, direction);
            Assert.Throws<InvalidOperationException>(() => line.Normalize());
            Assert.That(line.Origin, Is.EqualTo(Vector2.One));
            Assert.That(line.DirectionVector.X, Is.EqualTo(component));
            Assert.That(line.DirectionVector.Y, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => new AffineLine(Vector2.One, direction, true));
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(0.5f, 2f, 2f)]
        [TestCase(1f, 4f, 0f)]
        [TestCase(-1f, -4f, -16f)]
        [TestCase(2f, 8f, -16f)]
        public void BezierEvaluatesKnownCurveIncludingExtrapolation(float t, float x, float y)
        {
            IVectorFunction curve = new QuadraticBezier(Vector2.Zero, new Vector2(2f, 4f), new Vector2(4f, 0f));
            Assert.That(curve.Evaluate(t), Is.EqualTo(new Vector2(x, y)));
        }

        [Test]
        public void BezierPropertiesControlSubsequentEvaluation()
        {
            var curve = new QuadraticBezier();
            Assert.That(curve.Evaluate(0.5f), Is.EqualTo(Vector2.Zero));
            curve.P0 = new Vector2(2f, 4f);
            curve.P1 = new Vector2(4f, 8f);
            curve.P2 = new Vector2(6f, 4f);
            Assert.That(curve.Evaluate(0f), Is.EqualTo(curve.P0));
            Assert.That(curve.Evaluate(0.5f), Is.EqualTo(new Vector2(4f, 6f)));
            Assert.That(curve.Evaluate(1f), Is.EqualTo(curve.P2));
            curve.P1 = new Vector2(4f, 4f);
            Assert.That(curve.Evaluate(0.5f), Is.EqualTo(new Vector2(4f, 4f)));
        }
    }
}
