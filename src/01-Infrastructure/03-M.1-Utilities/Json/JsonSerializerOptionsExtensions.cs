using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlackThunder.UniEngine.Infrastructure.Utilities.Json
{
    public static class JsonSerializerOptionsExtensions
    {
        // Content
        public static JsonSerializerOptions AddUniEngineConverters(this JsonSerializerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            AddConverter(options, new Vector2JsonConverter());
            AddConverter(options, new Vector3JsonConverter());
            AddConverter(options, new Vector2IntJsonConverter());
            AddConverter(options, new Vector3IntJsonConverter());
            AddConverter(options, new QuaternionJsonConverter());
            AddConverter(options, new ColorJsonConverter());
            AddConverter(options, new Transform2DJsonConverter());
            AddConverter(options, new Transform3DJsonConverter());
            return options;
        }

        private static void AddConverter<T>(
            JsonSerializerOptions options,
            JsonConverter<T> converter)
        {
            foreach (JsonConverter registeredConverter in options.Converters)
            {
                if (registeredConverter.CanConvert(typeof(T)))
                    return;
            }

            options.Converters.Add(converter);
        }
    }
}
