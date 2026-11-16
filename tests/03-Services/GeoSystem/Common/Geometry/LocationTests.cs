using System;
using BlackThunder.GeoSystem.Geometry;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [Category("GeoSystem")]
    public class LocationTests
    {
        [TestCase(true, DistanceType.Length, 2f, 2f)]
        [TestCase(false, DistanceType.Length, 2f, 8f)]
        [TestCase(true, DistanceType.Rate, 0.2f, 2f)]
        [TestCase(false, DistanceType.Rate, 0.2f, 8f)]
        public void DirectionIsMeasuredFromTheSelectedEndpoint(
            bool forward, DistanceType type, float value, float expected)
        {
            var location = new Location(value, type, forward);
            Assert.That(location.ToDistance(10f), Is.EqualTo(expected).Within(1e-6f));
            Assert.That(location.ToRate(10f), Is.EqualTo(expected / 10f).Within(1e-6f));
        }

        [Test]
        public void DefaultAndEqualityPreserveTheForwardOrigin()
        {
            var origin = default(Location);
            Assert.That(origin, Is.EqualTo(new Location(0f)));
            Assert.That(origin.IsForward, Is.True);
            Assert.That(origin.DistanceType, Is.EqualTo(DistanceType.Length));
            Assert.That(origin.GetHashCode(), Is.EqualTo(new Location(0f).GetHashCode()));
            Assert.That(origin != new Location(0f, isForward: false), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Location(0f, (DistanceType)3));
        }
    }
}
