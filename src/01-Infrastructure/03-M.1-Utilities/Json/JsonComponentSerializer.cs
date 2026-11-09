using System;
using System.Text.Json;

namespace BlackThunder.UniEngine.Infrastructure.Utilities.Json
{
    internal static class JsonComponentSerializer
    {
        // Content
        public static void ReadSingleComponents(
            ref Utf8JsonReader reader,
            string[] componentNames,
            Span<float> components)
        {
            ValidateArguments(reader, componentNames, components.Length);
            Span<bool> encounteredComponents = stackalloc bool[componentNames.Length];
            encounteredComponents.Clear();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    ValidateRequiredComponents(componentNames, encounteredComponents);
                    return;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("A JSON component name was expected.");

                string componentName = reader.GetString();
                int componentIndex = GetComponentIndex(componentName, componentNames);
                if (componentIndex < 0)
                    throw new JsonException($"The '{componentName}' component is not supported.");

                if (encounteredComponents[componentIndex])
                    throw new JsonException($"The '{componentName}' component is duplicated.");

                if (!reader.Read()
                    || reader.TokenType != JsonTokenType.Number
                    || !reader.TryGetSingle(out float component)
                    || !float.IsFinite(component))
                {
                    throw new JsonException(
                        $"The '{componentName}' component must be a finite single-precision number.");
                }

                components[componentIndex] = component;
                encounteredComponents[componentIndex] = true;
            }

            throw new JsonException("The JSON object is incomplete.");
        }

        public static void ReadInt32Components(ref Utf8JsonReader reader, string[] componentNames, Span<int> components)
        {
            ValidateArguments(reader, componentNames, components.Length);
            Span<bool> encounteredComponents = stackalloc bool[componentNames.Length];
            encounteredComponents.Clear();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    ValidateRequiredComponents(componentNames, encounteredComponents);
                    return;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("A JSON component name was expected.");

                string componentName = reader.GetString();
                int componentIndex = GetComponentIndex(componentName, componentNames);
                if (componentIndex < 0)
                    throw new JsonException($"The '{componentName}' component is not supported.");

                if (encounteredComponents[componentIndex])
                    throw new JsonException($"The '{componentName}' component is duplicated.");

                if (!reader.Read()
                    || reader.TokenType != JsonTokenType.Number
                    || !reader.TryGetInt32(out int component))
                {
                    throw new JsonException($"The '{componentName}' component must be a 32-bit integer.");
                }

                components[componentIndex] = component;
                encounteredComponents[componentIndex] = true;
            }

            throw new JsonException("The JSON object is incomplete.");
        }

        public static float ReadSingle(ref Utf8JsonReader reader, string componentName)
        {
            if (reader.TokenType != JsonTokenType.Number
                || !reader.TryGetSingle(out float component)
                || !float.IsFinite(component))
            {
                throw new JsonException($"The '{componentName}' component must be a finite single-precision number.");
            }

            return component;
        }

        private static int GetComponentIndex(string componentName, string[] componentNames)
        {
            for (int index = 0; index < componentNames.Length; index++)
            {
                if (string.Equals(componentName, componentNames[index], StringComparison.OrdinalIgnoreCase))
                    return index;
            }

            return -1;
        }

        public static void WriteSingle(Utf8JsonWriter writer, string componentName, float component)
        {
            if (!float.IsFinite(component))
                throw new JsonException($"The '{componentName}' component must be finite.");

            writer.WriteNumber(componentName, component);
        }

        private static void ValidateArguments(Utf8JsonReader reader, string[] componentNames, int componentCount)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("A JSON object was expected.");

            if (componentNames.Length != componentCount)
                throw new ArgumentException("The component name and value counts must match.");
        }

        private static void ValidateRequiredComponents(
            string[] componentNames,
            ReadOnlySpan<bool> encounteredComponents)
        {
            for (int index = 0; index < componentNames.Length; index++)
            {
                if (!encounteredComponents[index])
                    throw new JsonException($"The '{componentNames[index]}' component is missing.");
            }
        }
    }
}
