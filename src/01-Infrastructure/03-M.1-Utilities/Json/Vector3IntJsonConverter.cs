using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.Infrastructure.Utilities.Json
{
    public class Vector3IntJsonConverter : JsonConverter<Vector3Int>
    {
        // Internal
        private static readonly string[] _componentNames = { "X", "Y", "Z" };


        // Content
        public override Vector3Int Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var components = new int[_componentNames.Length];
            JsonComponentSerializer.ReadInt32Components(ref reader, _componentNames, components);
            return new Vector3Int(components[0], components[1], components[2]);
        }

        public override void Write(
            Utf8JsonWriter writer,
            Vector3Int value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteNumber("Z", value.Z);
            writer.WriteEndObject();
        }
    }
}
