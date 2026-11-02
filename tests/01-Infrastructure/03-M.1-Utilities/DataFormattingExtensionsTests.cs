using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using BlackThunder.UniEngine.Infrastructure.Utilities;
using BlackThunder.UniEngine.Objects;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class DataFormattingExtensionsTests
    {
        // Internal
        private class NamedValue
        {
            public override string ToString() => "named";
        }


        // Content
        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(1000, "1K")]
        [TestCase(1250, "1.3K")]
        [TestCase(999999, "1000K")]
        [TestCase(1000000, "1M")]
        [TestCase(-1000, "-1K")]
        [TestCase(-1250000, "-1.3M")]
        [TestCase(int.MinValue, "-2147.5M")]
        [TestCase(int.MaxValue, "2147.5M")]
        public void CompactIntegersUseMagnitudeThresholds(int value, string expected) =>
            Assert.That(value.ToCompactString(), Is.EqualTo(expected));

        [TestCase(999.5f, "999.5")]
        [TestCase(1000f, "1K")]
        [TestCase(1250f, "1.3K")]
        [TestCase(1000000f, "1M")]
        [TestCase(-1500000f, "-1.5M")]
        [TestCase(float.NaN, "NaN")]
        [TestCase(float.PositiveInfinity, "Infinity")]
        [TestCase(float.NegativeInfinity, "-Infinity")]
        public void CompactSinglesPreserveSpecialValues(float value, string expected) =>
            Assert.That(value.ToCompactString(), Is.EqualTo(expected));

        [TestCase(0d, "0s")]
        [TestCase(0.9d, "0s")]
        [TestCase(59.9d, "59s")]
        [TestCase(60d, "1m 0s")]
        [TestCase(61d, "1m 1s")]
        [TestCase(3599d, "59m 59s")]
        [TestCase(3600d, "1h")]
        [TestCase(3661d, "1h 1m")]
        [TestCase(90061d, "25h 1m")]
        [TestCase(-61d, "-1m 1s")]
        [TestCase(-3661d, "-1h 1m")]
        public void DurationsUseTotalHoursAndWholeSeconds(double seconds, string expected) =>
            Assert.That(TimeSpan.FromSeconds(seconds).ToDisplayString(), Is.EqualTo(expected));

        [TestCase(false, "256204778h 48m")]
        [TestCase(true, "-256204778h 48m")]
        public void DurationExtremaPreserveTheirMinuteRemainder(bool negative, string expected)
        {
            var duration = negative ? TimeSpan.MinValue : TimeSpan.MaxValue;
            Assert.That(duration.ToDisplayString(), Is.EqualTo(expected));
        }

        [TestCase(false, "256204777h 59m")]
        [TestCase(true, "-256204777h 59m")]
        public void LargeDurationsDoNotRoundUpAcrossAnHourBoundary(bool negative, string expected)
        {
            var ticks = 256_204_778L * TimeSpan.TicksPerHour - 1;
            var duration = TimeSpan.FromTicks(negative ? -ticks : ticks);
            Assert.That(duration.ToDisplayString(), Is.EqualTo(expected));
        }

        [TestCase("fr-FR")]
        [TestCase("tr-TR")]
        public void NumericAndCompositeDisplaysUseInvariantCulture(string cultureName)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                Assert.That(1250f.ToCompactString(), Is.EqualTo("1.3K"));
                Assert.That(new Quaternion(1.25f, -2.5f, 3.75f, 4f).ToDisplayString(),
                    Is.EqualTo("X=1.25, Y=-2.5, Z=3.75, W=4"));
                Assert.That(new Transform2D(new Vector2(1.25f, -2.5f), 3.75f).ToDisplayString(),
                    Is.EqualTo("Position: <1.25, -2.5>, Rotation: 3.75"));
                Assert.That(new Transform3D(new Vector3(1.25f, -2.5f, 3.75f), Quaternion.Identity).ToDisplayString(),
                    Is.EqualTo("Position: <1.25, -2.5, 3.75>, Rotation: {X:0 Y:0 Z:0 W:1}"));
                Assert.That(new[] { 1.25f, -2.5f }.ToDisplayString(detailedFormat: false), Is.EqualTo("[1.25, -2.5]"));
                Assert.That(TimeSpan.FromSeconds(-61).ToDisplayString(), Is.EqualTo("-1m 1s"));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NullAndEmptyCollectionsHaveDistinctDisplays(bool detailed)
        {
            Assert.That(((IEnumerable<int>)null).ToDisplayString(detailedFormat: detailed), Is.EqualTo("null"));
            var empty = Array.Empty<int>();
            Assert.That(empty.ToDisplayString(detailedFormat: detailed),
                Is.EqualTo(detailed ? "System.Int32[], Count: 0" : "[]"));
        }

        [Test]
        public void SimpleCollectionsPreserveOrderNullsAndObjectRepresentations()
        {
            var values = new object[] { 1.25f, null, "text", new NamedValue() };
            Assert.That(values.ToDisplayString(detailedFormat: false), Is.EqualTo("[1.25, null, text, named]"));
        }

        [Test]
        public void DetailedCollectionsIncludeTypeCountIndicesAndSelectedValues()
        {
            var values = new[] { 2, 3 };
            var expected = "System.Int32[], Count: 2" + Environment.NewLine
                + "=======" + Environment.NewLine
                + "[0] 2.5" + Environment.NewLine
                + "[1] 3.75" + Environment.NewLine
                + "=======";
            Assert.That(values.ToDisplayString(value => value * 1.25f), Is.EqualTo(expected));
        }

        [Test]
        public void SelectorsAreCalledOncePerElementAndMayReturnNull()
        {
            var selected = new List<int>();
            var result = new[] { 1, 2, 3 }.ToDisplayString(value =>
            {
                selected.Add(value);
                return value == 2 ? null : (object)(value * 10);
            }, detailedFormat: false);

            Assert.That(selected, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(result, Is.EqualTo("[10, null, 30]"));
        }

        [Test]
        public void NonListCollectionsAreEnumeratedOnceBeforeFormatting()
        {
            var enumerations = 0;
            IEnumerable<int> Values()
            {
                enumerations++;
                if (enumerations > 1)
                    throw new InvalidOperationException("The source can only be enumerated once.");
                yield return 3;
                yield return 1;
            }

            Assert.That(Values().ToDisplayString(detailedFormat: false), Is.EqualTo("[3, 1]"));
            Assert.That(enumerations, Is.EqualTo(1));
        }

        [Test]
        public void EnumerationAndSelectorFailuresPropagateToTheCaller()
        {
            var failure = new InvalidOperationException("source failure");
            IEnumerable<int> BrokenValues()
            {
                yield return 1;
                throw failure;
            }

            Assert.That(Assert.Throws<InvalidOperationException>(() => BrokenValues().ToDisplayString()), Is.SameAs(failure));
            Assert.That(Assert.Throws<InvalidOperationException>(() => new[] { 1 }.ToDisplayString(_ => throw failure)),
                Is.SameAs(failure));
        }
    }
}
