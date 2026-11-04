using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using BlackThunder.UniEngine.Objects;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class InfrastructureObjectTests
    {
        // Content
        [Test]
        public void Vector2IntConstructionAndCopyPreserveIndependentComponents()
        {
            Assert.That(default(Vector2Int), Is.EqualTo(Vector2Int.Zero));
            var original = new Vector2Int(int.MinValue, int.MaxValue);
            var copy = original;
            copy.X = 7;
            copy.Y = -8;

            Assert.That(original.X, Is.EqualTo(int.MinValue));
            Assert.That(original.Y, Is.EqualTo(int.MaxValue));
            Assert.That(copy, Is.EqualTo(new Vector2Int(7, -8)));
            Assert.That(Vector2Int.Zero, Is.EqualTo(new Vector2Int(0, 0)));
        }

        [Test]
        public void Vector3IntConstructionAndCopyPreserveIndependentComponents()
        {
            Assert.That(default(Vector3Int), Is.EqualTo(Vector3Int.Zero));
            var original = new Vector3Int(int.MinValue, 3, int.MaxValue);
            var copy = original;
            copy.X = 7;
            copy.Y = -8;
            copy.Z = 9;

            Assert.That(original.X, Is.EqualTo(int.MinValue));
            Assert.That(original.Y, Is.EqualTo(3));
            Assert.That(original.Z, Is.EqualTo(int.MaxValue));
            Assert.That(copy, Is.EqualTo(new Vector3Int(7, -8, 9)));
            Assert.That(Vector3Int.Zero, Is.EqualTo(new Vector3Int(0, 0, 0)));
        }

        [Test]
        public void Transform2DConstructionAndCopyPreserveRawPositionAndRadians()
        {
            Assert.That(default(Transform2D), Is.EqualTo(Transform2D.Identity));
            Assert.That(Transform2D.Identity.Position, Is.EqualTo(Vector2.Zero));
            Assert.That(Transform2D.Identity.Rotation, Is.Zero);
            var original = new Transform2D(new Vector2(1.25f, -2.5f), 7.5f);
            var copy = original;
            copy.Position = new Vector2(-3f, 4f);
            copy.Rotation = -12.5f;

            Assert.That(original.Position, Is.EqualTo(new Vector2(1.25f, -2.5f)));
            Assert.That(original.Rotation, Is.EqualTo(7.5f));
            Assert.That(copy, Is.EqualTo(new Transform2D(new Vector2(-3f, 4f), -12.5f)));
        }

        [Test]
        public void Transform3DConstructionAndCopyPreserveUnnormalizedQuaternion()
        {
            Assert.That(default(Transform3D).Position, Is.EqualTo(Vector3.Zero));
            Assert.That(default(Transform3D).Rotation, Is.EqualTo(default(Quaternion)));
            Assert.That(Transform3D.Identity.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(Transform3D.Identity.Rotation, Is.EqualTo(Quaternion.Identity));
            var original = new Transform3D(new Vector3(1f, -2f, 3f), new Quaternion(4f, 5f, 6f, 7f));
            var copy = original;
            copy.Position = new Vector3(-8f, 9f, 10f);
            copy.Rotation = new Quaternion(11f, 12f, 13f, 14f);

            Assert.That(original.Position, Is.EqualTo(new Vector3(1f, -2f, 3f)));
            Assert.That(original.Rotation, Is.EqualTo(new Quaternion(4f, 5f, 6f, 7f)));
            Assert.That(copy.Position, Is.EqualTo(new Vector3(-8f, 9f, 10f)));
            Assert.That(copy.Rotation, Is.EqualTo(new Quaternion(11f, 12f, 13f, 14f)));
        }

        [Test]
        public void Vector2IntEqualityUsesEveryComponentAndSupportsHashKeys()
        {
            var value = new Vector2Int(1, 2);
            var equal = new Vector2Int(1, 2);
            AssertEqualityContract(value, equal);
            Assert.That(value == equal, Is.True);
            Assert.That(value != equal, Is.False);
            foreach (var other in new[] { new Vector2Int(-1, 2), new Vector2Int(1, -2) })
            {
                Assert.That(value.Equals(other), Is.False);
                Assert.That(value == other, Is.False);
                Assert.That(value != other, Is.True);
            }
        }

        [Test]
        public void Vector3IntEqualityUsesEveryComponentAndSupportsHashKeys()
        {
            var value = new Vector3Int(1, 2, 3);
            var equal = new Vector3Int(1, 2, 3);
            AssertEqualityContract(value, equal);
            Assert.That(value == equal, Is.True);
            Assert.That(value != equal, Is.False);
            foreach (var other in new[] { new Vector3Int(-1, 2, 3), new Vector3Int(1, -2, 3), new Vector3Int(1, 2, -3) })
            {
                Assert.That(value.Equals(other), Is.False);
                Assert.That(value == other, Is.False);
                Assert.That(value != other, Is.True);
            }
        }

        [Test]
        public void Transform2DEqualityUsesPositionAndRotationAndSupportsHashKeys()
        {
            var value = new Transform2D(new Vector2(1f, 2f), 3f);
            var equal = new Transform2D(new Vector2(1f, 2f), 3f);
            AssertEqualityContract(value, equal);
            Assert.That(value == equal, Is.True);
            Assert.That(value != equal, Is.False);
            foreach (var other in new[]
            {
                new Transform2D(new Vector2(-1f, 2f), 3f),
                new Transform2D(new Vector2(1f, -2f), 3f),
                new Transform2D(new Vector2(1f, 2f), -3f),
            })
            {
                Assert.That(value.Equals(other), Is.False);
                Assert.That(value == other, Is.False);
                Assert.That(value != other, Is.True);
            }
        }

        [Test]
        public void Transform3DEqualityUsesPositionAndQuaternionAndSupportsHashKeys()
        {
            var value = new Transform3D(new Vector3(1f, 2f, 3f), new Quaternion(4f, 5f, 6f, 7f));
            var equal = new Transform3D(new Vector3(1f, 2f, 3f), new Quaternion(4f, 5f, 6f, 7f));
            AssertEqualityContract(value, equal);
            Assert.That(value == equal, Is.True);
            Assert.That(value != equal, Is.False);
            foreach (var other in new[]
            {
                new Transform3D(new Vector3(-1f, 2f, 3f), equal.Rotation),
                new Transform3D(new Vector3(1f, -2f, 3f), equal.Rotation),
                new Transform3D(new Vector3(1f, 2f, -3f), equal.Rotation),
                new Transform3D(equal.Position, new Quaternion(-4f, 5f, 6f, 7f)),
                new Transform3D(equal.Position, new Quaternion(4f, -5f, 6f, 7f)),
                new Transform3D(equal.Position, new Quaternion(4f, 5f, -6f, 7f)),
                new Transform3D(equal.Position, new Quaternion(4f, 5f, 6f, -7f)),
            })
            {
                Assert.That(value.Equals(other), Is.False);
                Assert.That(value == other, Is.False);
                Assert.That(value != other, Is.True);
            }
        }

        [TestCase("fr-FR")]
        [TestCase("tr-TR")]
        public void ObjectStringsAreIndependentOfCurrentCulture(string cultureName)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                Assert.That(new Vector2Int(-1, 2).ToString(), Is.EqualTo("<-1, 2>"));
                Assert.That(new Vector3Int(-1, 2, 3).ToString(), Is.EqualTo("<-1, 2, 3>"));
                Assert.That(new Transform2D(new Vector2(1.25f, -2.5f), 3.75f).ToString(),
                    Is.EqualTo("Position: <1.25, -2.5>, Rotation: 3.75"));
                Assert.That(new Transform3D(new Vector3(1.25f, -2.5f, 3.75f), new Quaternion(0f, 0f, 0f, 1f)).ToString(),
                    Is.EqualTo("Position: <1.25, -2.5, 3.75>, Rotation: {X:0 Y:0 Z:0 W:1}"));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        private static void AssertEqualityContract<T>(T value, T equal) where T : struct, IEquatable<T>
        {
            Assert.That(value.Equals(equal), Is.True);
            Assert.That(equal.Equals(value), Is.True);
            Assert.That(value.Equals((object)equal), Is.True);
            Assert.That(value.Equals(null), Is.False);
            Assert.That(value.Equals("unrelated"), Is.False);
            Assert.That(value.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            var lookup = new Dictionary<T, string> { [value] = "stored" };
            Assert.That(lookup[equal], Is.EqualTo("stored"));
        }
    }
}
