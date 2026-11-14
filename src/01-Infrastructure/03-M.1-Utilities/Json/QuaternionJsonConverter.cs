using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlackThunder.UniEngine.Utilities.Json
{
    public class QuaternionJsonConverter : JsonConverter<Quaternion>
    {
        // Internal
        private static readonly string[] _componentNames = { "X", "Y", "Z", "W" };


        // Content
        public override Quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var components = new float[_componentNames.Length];
            JsonComponentSerializer.ReadSingleComponents(ref reader, _componentNames, components);
            return new Quaternion(components[0], components[1], components[2], components[3]);
        }

        public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            JsonComponentSerializer.WriteSingle(writer, "X", value.X);
            JsonComponentSerializer.WriteSingle(writer, "Y", value.Y);
            JsonComponentSerializer.WriteSingle(writer, "Z", value.Z);
            JsonComponentSerializer.WriteSingle(writer, "W", value.W);
            writer.WriteEndObject();
        }
    }
}
