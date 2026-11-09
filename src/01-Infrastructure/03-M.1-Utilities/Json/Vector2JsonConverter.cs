using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlackThunder.UniEngine.Infrastructure.Utilities.Json
{
    public class Vector2JsonConverter : JsonConverter<Vector2>
    {
        // Internal
        private static readonly string[] _componentNames = { "X", "Y" };


        // Content
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var components = new float[_componentNames.Length];
            JsonComponentSerializer.ReadSingleComponents(ref reader, _componentNames, components);
            return new Vector2(components[0], components[1]);
        }

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            JsonComponentSerializer.WriteSingle(writer, "X", value.X);
            JsonComponentSerializer.WriteSingle(writer, "Y", value.Y);
            writer.WriteEndObject();
        }
    }
}
