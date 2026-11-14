using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlackThunder.UniEngine.Utilities.Json;
using BlackThunder.UniEngine.Objects;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class JsonSerializerOptionsExtensionsTests
    {
        // Internal
        private class CustomVector2Converter : JsonConverter<Vector2>
        {
            public override Vector2 Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options) =>
                reader.TokenType == JsonTokenType.String && reader.GetString() == "custom"
                    ? new Vector2(9, 8)
                    : throw new JsonException("A custom vector string was expected.");

            public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options) =>
                writer.WriteStringValue("custom");
        }


        // Content
        [Test]
        public void NullOptionsAreRejected() =>
            Assert.That(
                Assert.Throws<ArgumentNullException>(
                    () => ((JsonSerializerOptions)null).AddUniEngineConverters()).ParamName,
                Is.EqualTo("options"));

        [Test]
        public void RegistrationReturnsTheSameOptionsAndAddsEachConverterOnlyOnce()
        {
            var options = new JsonSerializerOptions();
            Assert.That(options.AddUniEngineConverters(), Is.SameAs(options));
            var original = options.Converters.ToArray();

            Assert.That(original.Select(converter => converter.GetType()), Is.EqualTo(new[]
            {
                typeof(Vector2JsonConverter), typeof(Vector3JsonConverter), typeof(Vector2IntJsonConverter),
                typeof(Vector3IntJsonConverter), typeof(QuaternionJsonConverter), typeof(ColorJsonConverter),
                typeof(Transform2DJsonConverter), typeof(Transform3DJsonConverter),
            }));

            options.AddUniEngineConverters();
            Assert.That(options.Converters, Is.EqualTo(original));
        }

        [Test]
        public void ExistingCompatibleConvertersRetainTheirPositionAndBehavior()
        {
            var custom = new CustomVector2Converter();
            var options = new JsonSerializerOptions();
            options.Converters.Add(custom);
            options.AddUniEngineConverters();
            Assert.That(options.Converters.Count, Is.EqualTo(8));
            Assert.That(options.Converters[0], Is.SameAs(custom));
            Assert.That(options.Converters.OfType<Vector2JsonConverter>(), Is.Empty);
            Assert.That(JsonSerializer.Serialize(new Vector2(1, 2), options), Is.EqualTo("\"custom\""));
            Assert.That(JsonSerializer.Deserialize<Vector2>("\"custom\"", options), Is.EqualTo(new Vector2(9, 8)));
        }

        [Test]
        public void TransformComponentsUseTheirDeclaredConvertersInsteadOfTopLevelOverrides()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new CustomVector2Converter());
            options.AddUniEngineConverters();
            var transform = new Transform2D(new Vector2(1, 2), 3);
            const string Expected = "{\"Position\":{\"X\":1,\"Y\":2},\"Rotation\":3}";
            Assert.That(JsonSerializer.Serialize(transform, options), Is.EqualTo(Expected));
            Assert.That(JsonSerializer.Deserialize<Transform2D>(Expected, options), Is.EqualTo(transform));
        }
    }
}
