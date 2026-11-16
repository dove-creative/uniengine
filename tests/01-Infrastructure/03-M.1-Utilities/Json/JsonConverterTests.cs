using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using BlackThunder.UniEngine.Utilities.Json;
using BlackThunder.UniEngine.Objects;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class JsonConverterTests
    {
        // Content --------------------
        // Valid representations
        [TestCaseSource(nameof(ValidValues))]
        public void EveryConverterWritesTheDeclaredShapeAndRestoresTheValue(Type subjectType, object value, string json)
        {
            var options = new JsonSerializerOptions().AddUniEngineConverters();
            Assert.That(JsonSerializer.Serialize(value, subjectType, options), Is.EqualTo(json));
            AssertValue(JsonSerializer.Deserialize(json, subjectType, options), value);
        }

        [TestCaseSource(nameof(ValidValues))]
        public void PropertyOrderAndCaseDoNotChangeTheValue(Type subjectType, object value, string json)
        {
            var options = new JsonSerializerOptions().AddUniEngineConverters();
            var reordered = ReversePropertiesAndLowerCaseNames(json);
            AssertValue(JsonSerializer.Deserialize(reordered, subjectType, options), value);
        }

        [Test]
        public void Transform2DRotationRoundTripsDegreesWithoutNormalizing()
        {
            var options = new JsonSerializerOptions().AddUniEngineConverters();
            const string Json = "{\"Position\":{\"X\":0,\"Y\":0},\"Rotation\":450}";
            var value = JsonSerializer.Deserialize<Transform2D>(Json, options);

            Assert.That(value.Rotation, Is.EqualTo(450f));
            Assert.That(value.Is(new Transform2D(Vector2.Zero, 90f), 0f, 0f), Is.True);
            Assert.That(JsonSerializer.Serialize(value, options), Is.EqualTo(Json));
        }

        [Test]
        public void IntegerComponentsRetainBothInt32Boundaries()
        {
            var options = new JsonSerializerOptions().AddUniEngineConverters();
            var value2 = new Vector2Int(int.MinValue, int.MaxValue);
            var value3 = new Vector3Int(int.MaxValue, int.MinValue, 0);
            Assert.That(
                JsonSerializer.Deserialize<Vector2Int>(JsonSerializer.Serialize(value2, options), options),
                Is.EqualTo(value2));
            Assert.That(
                JsonSerializer.Deserialize<Vector3Int>(JsonSerializer.Serialize(value3, options), options),
                Is.EqualTo(value3));
        }

        [Test]
        public void FiniteSingleBoundariesAndSubnormalValuesRoundTrip()
        {
            var options = new JsonSerializerOptions().AddUniEngineConverters();
            var value = new Vector3(float.MinValue, float.MaxValue, float.Epsilon);
            Assert.That(
                JsonSerializer.Deserialize<Vector3>(JsonSerializer.Serialize(value, options), options),
                Is.EqualTo(value));
        }

        [Test]
        public void ColorNormalizesBytesAndQuantizesArbitraryComponents()
        {
            var options = new JsonSerializerOptions().AddUniEngineConverters();
            var original = Color.FromArgb(128, 64, 192, 255);

            var json = JsonSerializer.Serialize(original, options);
            using (var document = JsonDocument.Parse(json))
            {
                var root = document.RootElement;
                Assert.That(root.GetProperty("R").GetSingle(), Is.EqualTo(64f / 255f));
                Assert.That(root.GetProperty("G").GetSingle(), Is.EqualTo(192f / 255f));
                Assert.That(root.GetProperty("B").GetSingle(), Is.EqualTo(1f));
                Assert.That(root.GetProperty("A").GetSingle(), Is.EqualTo(128f / 255f));
            }

            Assert.That(JsonSerializer.Deserialize<Color>(json, options).ToArgb(), Is.EqualTo(original.ToArgb()));
            var quantized = JsonSerializer.Deserialize<Color>("{\"R\":0.5,\"G\":0,\"B\":1,\"A\":0.5}", options);
            Assert.That(quantized.ToArgb(), Is.EqualTo(Color.FromArgb(128, 128, 0, 255).ToArgb()));
        }

        // Invalid representations
        [TestCaseSource(nameof(InvalidObjects))]
        public void InvalidShapesAndComponentsAreRejected(Type subjectType, string json) =>
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
                json,
                subjectType,
                new JsonSerializerOptions().AddUniEngineConverters()));

        [TestCaseSource(nameof(NonFiniteValues))]
        public void NonFiniteValuesCannotBeWritten(Type subjectType, object value) =>
            Assert.Throws<JsonException>(() => JsonSerializer.Serialize(
                value,
                subjectType,
                new JsonSerializerOptions().AddUniEngineConverters()));

        // Case sources
        private static IEnumerable<TestCaseData> ValidValues()
        {
            foreach (var entry in Representations())
                yield return new TestCaseData(entry.Type, entry.Value, entry.Json).SetName("{m}" + entry.Type.Name);
        }

        private static IEnumerable<TestCaseData> InvalidObjects()
        {
            foreach (var entry in Representations())
            {
                foreach (var invalidRoot in new[] { "null", "[]", "0", "\"value\"", "true" })
                    yield return Invalid(entry.Type, invalidRoot, "Root" + invalidRoot);

                using (var document = JsonDocument.Parse(entry.Json))
                {
                    var properties = document.RootElement.EnumerateObject().ToArray();
                    for (int index = 0; index < properties.Length; index++)
                    {
                        var property = properties[index];
                        var omitted = properties.Where((item, position) => position != index)
                            .Select(item => item.ToString());
                        yield return Invalid(
                            entry.Type,
                            "{" + string.Join(",", omitted) + "}",
                            "Missing" + property.Name);
                        yield return Invalid(
                            entry.Type,
                            entry.Json.Insert(
                                1,
                                "\"" + property.Name.ToLowerInvariant() + "\":" + property.Value.GetRawText() + ","),
                            "Duplicate" + property.Name);

                        foreach (var invalidValue in new[] { "null", "\"1\"", "true", "[]" })
                        {
                            var changed = properties.Select((item, position) =>
                                "\"" + item.Name + "\":"
                                + (position == index ? invalidValue : item.Value.GetRawText()));
                            yield return Invalid(
                                entry.Type,
                                "{" + string.Join(",", changed) + "}",
                                "WrongType" + property.Name + invalidValue);
                        }
                    }
                }

                yield return Invalid(entry.Type, entry.Json.Insert(1, "\"Unexpected\":0,"), "Unknown");
                yield return Invalid(entry.Type, entry.Json.Substring(0, entry.Json.Length - 1), "Incomplete");
            }

            foreach (var subjectType in new[] { typeof(Vector2), typeof(Vector3), typeof(Quaternion) })
            {
                string remaining = subjectType == typeof(Vector2) ? "\"Y\":0" : "\"Y\":0,\"Z\":0";
                if (subjectType == typeof(Quaternion)) remaining += ",\"W\":1";

                foreach (var invalidNumber in new[] { "1e100", "-1e100", "\"NaN\"", "\"Infinity\"" })
                    yield return Invalid(
                        subjectType,
                        "{\"X\":" + invalidNumber + "," + remaining + "}",
                        "NonFinite" + invalidNumber);
            }

            foreach (var subjectType in new[] { typeof(Vector2Int), typeof(Vector3Int) })
            {
                string remaining = subjectType == typeof(Vector2Int) ? "\"Y\":0" : "\"Y\":0,\"Z\":0";
                foreach (var invalidNumber in new[] { "2147483648", "-2147483649", "1.5", "1e100" })
                    yield return Invalid(
                        subjectType,
                        "{\"X\":" + invalidNumber + "," + remaining + "}",
                        "Int32Range" + invalidNumber);
            }

            foreach (var component in new[] { "R", "G", "B", "A" })
            {
                foreach (var invalidNumber in new[] { "-0.01", "1.01", "1e100" })
                {
                    var components = new[] { "R", "G", "B", "A" }.Select(
                        name => "\"" + name + "\":" + (name == component ? invalidNumber : "0"));
                    yield return Invalid(
                        typeof(Color),
                        "{" + string.Join(",", components) + "}",
                        "Range" + component + invalidNumber);
                }
            }

            yield return Invalid(typeof(Transform2D), "{\"Position\":{\"X\":0},\"Rotation\":0}", "NestedMissing");
            yield return Invalid(
                typeof(Transform2D),
                "{\"Position\":{\"X\":1e100,\"Y\":0},\"Rotation\":0}",
                "NestedNonFinite");
            yield return Invalid(
                typeof(Transform2D),
                "{\"Position\":{\"X\":0,\"Y\":0},\"Rotation\":1e100}",
                "RotationNonFinite");
            yield return Invalid(
                typeof(Transform3D),
                "{\"Position\":{\"X\":0,\"Y\":0,\"Z\":0},\"Rotation\":{\"X\":0,\"Y\":0,\"Z\":0}}",
                "NestedMissing");
            yield return Invalid(
                typeof(Transform3D),
                "{\"Position\":{\"X\":0,\"Y\":0,\"Z\":0},\"Rotation\":{\"X\":0,\"Y\":0,\"Z\":0,\"W\":1e100}}",
                "NestedNonFinite");
        }

        private static IEnumerable<TestCaseData> NonFiniteValues()
        {
            foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                string suffix = value.ToString(CultureInfo.InvariantCulture);
                yield return new TestCaseData(typeof(Vector2), new Vector2(value, 0))
                    .SetName("JsonWriteVector2" + suffix);
                yield return new TestCaseData(typeof(Vector3), new Vector3(0, 0, value))
                    .SetName("JsonWriteVector3" + suffix);
                yield return new TestCaseData(typeof(Quaternion), new Quaternion(0, value, 0, 1))
                    .SetName("JsonWriteQuaternion" + suffix);
                yield return new TestCaseData(typeof(Transform2D), new Transform2D(Vector2.Zero, value))
                    .SetName("JsonWriteTransform2DRotation" + suffix);
                yield return new TestCaseData(typeof(Transform2D), new Transform2D(new Vector2(value, 0), 0))
                    .SetName("JsonWriteTransform2DPosition" + suffix);
                yield return new TestCaseData(
                    typeof(Transform3D),
                    new Transform3D(new Vector3(0, value, 0), Quaternion.Identity))
                    .SetName("JsonWriteTransform3DPosition" + suffix);
                yield return new TestCaseData(
                    typeof(Transform3D),
                    new Transform3D(Vector3.Zero, new Quaternion(0, 0, 0, value)))
                    .SetName("JsonWriteTransform3DRotation" + suffix);
            }
        }

        private static IEnumerable<(Type Type, object Value, string Json)> Representations()
        {
            yield return (typeof(Vector2), new Vector2(1.5f, -2), "{\"X\":1.5,\"Y\":-2}");
            yield return (typeof(Vector3), new Vector3(1.5f, -2, 3), "{\"X\":1.5,\"Y\":-2,\"Z\":3}");
            yield return (typeof(Vector2Int), new Vector2Int(1, -2), "{\"X\":1,\"Y\":-2}");
            yield return (typeof(Vector3Int), new Vector3Int(1, -2, 3), "{\"X\":1,\"Y\":-2,\"Z\":3}");
            yield return (typeof(Quaternion), new Quaternion(1, -2, 3, -4), "{\"X\":1,\"Y\":-2,\"Z\":3,\"W\":-4}");
            yield return (typeof(Color), Color.FromArgb(255, 255, 0, 255), "{\"R\":1,\"G\":0,\"B\":1,\"A\":1}");
            yield return (
                typeof(Transform2D),
                new Transform2D(new Vector2(1.5f, -2), 0.5f),
                "{\"Position\":{\"X\":1.5,\"Y\":-2},\"Rotation\":0.5}");
            yield return (
                typeof(Transform3D),
                new Transform3D(new Vector3(1.5f, -2, 3), new Quaternion(1, -2, 3, -4)),
                "{\"Position\":{\"X\":1.5,\"Y\":-2,\"Z\":3},\"Rotation\":{\"X\":1,\"Y\":-2,\"Z\":3,\"W\":-4}}");
        }

        private static TestCaseData Invalid(Type subjectType, string json, string reason) =>
            new TestCaseData(subjectType, json).SetName("JsonReject" + subjectType.Name + reason);

        private static void AssertValue(object actual, object expected)
        {
            if (expected is Color color)
                Assert.That(((Color)actual).ToArgb(), Is.EqualTo(color.ToArgb()));
            else
                Assert.That(actual, Is.EqualTo(expected));
        }

        private static string ReversePropertiesAndLowerCaseNames(string json)
        {
            using (var document = JsonDocument.Parse(json))
                return RewriteObject(document.RootElement);
        }

        private static string RewriteObject(JsonElement element) =>
            "{" + string.Join(
                ",",
                element.EnumerateObject().Reverse().Select(property =>
                    "\"" + property.Name.ToLowerInvariant() + "\":"
                    + (property.Value.ValueKind == JsonValueKind.Object
                        ? RewriteObject(property.Value)
                        : property.Value.GetRawText()))) + "}";
    }
}
