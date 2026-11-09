using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.Infrastructure.Utilities.Json
{
    public class Transform2DJsonConverter : JsonConverter<Transform2D>
    {
        // Internal
        private readonly Vector2JsonConverter _vectorConverter = new();


        // Content
        public override Transform2D Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("A JSON object was expected.");

            Vector2 position = default;
            float rotation = default;
            bool hasPosition = false;
            bool hasRotation = false;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    if (!hasPosition)
                        throw new JsonException("The 'Position' component is missing.");

                    if (!hasRotation)
                        throw new JsonException("The 'Rotation' component is missing.");

                    return new Transform2D(position, rotation);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("A JSON component name was expected.");

                string componentName = reader.GetString();

                if (!reader.Read())
                    throw new JsonException("The JSON object is incomplete.");

                if (string.Equals(componentName, "Position", StringComparison.OrdinalIgnoreCase))
                {
                    if (hasPosition)
                        throw new JsonException($"The '{componentName}' component is duplicated.");

                    position = _vectorConverter.Read(ref reader, typeof(Vector2), options);
                    hasPosition = true;
                }
                else if (string.Equals(componentName, "Rotation", StringComparison.OrdinalIgnoreCase))
                {
                    if (hasRotation)
                        throw new JsonException($"The '{componentName}' component is duplicated.");

                    rotation = JsonComponentSerializer.ReadSingle(ref reader, componentName);
                    hasRotation = true;
                }
                else
                {
                    throw new JsonException($"The '{componentName}' component is not supported.");
                }
            }

            throw new JsonException("The JSON object is incomplete.");
        }

        public override void Write(Utf8JsonWriter writer, Transform2D value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Position");
            _vectorConverter.Write(writer, value.Position, options);
            JsonComponentSerializer.WriteSingle(writer, "Rotation", value.Rotation);
            writer.WriteEndObject();
        }
    }
}
