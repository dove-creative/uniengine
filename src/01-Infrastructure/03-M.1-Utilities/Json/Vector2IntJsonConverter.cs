using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.Utilities.Json
{
    public class Vector2IntJsonConverter : JsonConverter<Vector2Int>
    {
        // Internal
        private static readonly string[] _componentNames = { "X", "Y" };


        // Content
        public override Vector2Int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var components = new int[_componentNames.Length];
            JsonComponentSerializer.ReadInt32Components(ref reader, _componentNames, components);
            return new Vector2Int(components[0], components[1]);
        }

        public override void Write(Utf8JsonWriter writer, Vector2Int value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteEndObject();
        }
    }
}
