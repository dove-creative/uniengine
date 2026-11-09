using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.UniEngine.Infrastructure.Utilities;

namespace BlackThunder.UniEngine.Infrastructure
{
    public static partial class Log
    {
        public static string Write(LogLevel logLevel = LogLevel.Log) => Write("Called", logLevel);

        public static string Write<T>(this T value, LogLevel logLevel = LogLevel.Log, bool smartWrite = true)
        {
            if (smartWrite && value is IEnumerable collection && value is not string)
                return WriteCollection(collection.Cast<object>(), logLevel);

            return Write(value?.ToString() ?? "null", logLevel);
        }

        public static string WriteCollection<T>(
            this IEnumerable<T> collection,
            LogLevel logLevel = LogLevel.Log,
            bool detailedFormat = false)
        {
            return Write(collection.ToDisplayString(detailedFormat: detailedFormat), logLevel);
        }

        public static string WriteCollection<T>(
            this IEnumerable<T> collection,
            Func<T, object> selector,
            LogLevel logLevel = LogLevel.Log,
            bool detailedFormat = false)
        {
            return Write(collection.ToDisplayString(selector, detailedFormat), logLevel);
        }
    }
}
