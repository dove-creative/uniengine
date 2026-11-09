using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlackThunder.UniEngine.Infrastructure.Utilities.Json
{
    public class Vector3JsonConverter : JsonConverter<Vector3>
    {
        // Internal
        private static readonly string[] _componentNames = { "X", "Y", "Z" };


        // Content
        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var components = new float[_componentNames.Length];
            JsonComponentSerializer.ReadSingleComponents(ref reader, _componentNames, components);
            return new Vector3(components[0], components[1], components[2]);
        }

        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            JsonComponentSerializer.WriteSingle(writer, "X", value.X);
            JsonComponentSerializer.WriteSingle(writer, "Y", value.Y);
            JsonComponentSerializer.WriteSingle(writer, "Z", value.Z);
            writer.WriteEndObject();
        }
    }
}
