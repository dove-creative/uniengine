using System;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlackThunder.UniEngine.Infrastructure.Utilities.Json
{
    public class ColorJsonConverter : JsonConverter<Color>
    {
        // Internal
        private static readonly string[] _componentNames = ["R", "G", "B", "A"];


        // Content
        public override Color Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            Span<float> components = stackalloc float[_componentNames.Length];
            JsonComponentSerializer.ReadSingleComponents(ref reader, _componentNames, components);

            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] < 0f
                    || components[index] > 1f)
                {
                    throw new JsonException($"The '{_componentNames[index]}' component must be between 0 and 1.");
                }
            }

            return Color.FromArgb(
                ToByte(components[3]),
                ToByte(components[0]),
                ToByte(components[1]),
                ToByte(components[2]));
        }

        public override void Write(
            Utf8JsonWriter writer,
            Color value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            JsonComponentSerializer.WriteSingle(writer, "R", ToSingle(value.R));
            JsonComponentSerializer.WriteSingle(writer, "G", ToSingle(value.G));
            JsonComponentSerializer.WriteSingle(writer, "B", ToSingle(value.B));
            JsonComponentSerializer.WriteSingle(writer, "A", ToSingle(value.A));
            writer.WriteEndObject();
        }

        private static byte ToByte(float component) =>
            (byte)MathF.Round(component * byte.MaxValue, MidpointRounding.AwayFromZero);

        private static float ToSingle(byte component) =>
            component / (float)byte.MaxValue;
    }
}
