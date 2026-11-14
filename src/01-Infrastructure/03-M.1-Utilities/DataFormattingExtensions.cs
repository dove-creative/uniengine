using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using BlackThunder.UniEngine.Objects;

namespace BlackThunder.UniEngine.Utilities
{
    public static class DataFormattingExtensions
    {
        // Content
        public static string ToCompactString(this int value) => FormatCompact(value);

        public static string ToCompactString(this float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return value.ToString(CultureInfo.InvariantCulture);

            return FormatCompact(value);
        }

        private static string FormatCompact(double value)
        {
            double magnitude = Math.Abs(value);
            if (magnitude >= 1_000_000d)
                return (value / 1_000_000d).ToString("0.#", CultureInfo.InvariantCulture) + "M";

            if (magnitude >= 1_000d)
                return (value / 1_000d).ToString("0.#", CultureInfo.InvariantCulture) + "K";

            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        public static string ToDisplayString(this TimeSpan value)
        {
            bool isNegative = value < TimeSpan.Zero;
            // Divide before taking the magnitude so TimeSpan.MinValue cannot overflow.
            long totalHours = Math.Abs(value.Ticks / TimeSpan.TicksPerHour);
            int minutes = Math.Abs(value.Minutes);
            int seconds = Math.Abs(value.Seconds);
            string sign = isNegative ? "-" : string.Empty;

            if (totalHours > 0)
            {
                string result = sign + totalHours.ToString(CultureInfo.InvariantCulture) + "h";
                if (minutes > 0)
                    result += " " + minutes.ToString(CultureInfo.InvariantCulture) + "m";

                return result;
            }

            if (minutes > 0)
            {
                return sign
                    + minutes.ToString(CultureInfo.InvariantCulture)
                    + "m "
                    + seconds.ToString(CultureInfo.InvariantCulture)
                    + "s";
            }

            return sign + seconds.ToString(CultureInfo.InvariantCulture) + "s";
        }

        public static string ToDisplayString(this Quaternion value) =>
            string.Format(
                CultureInfo.InvariantCulture,
                "X={0:G}, Y={1:G}, Z={2:G}, W={3:G}",
                value.X,
                value.Y,
                value.Z,
                value.W);

        public static string ToDisplayString(this Transform2D value) => value.ToString();

        public static string ToDisplayString(this Transform3D value) => value.ToString();

        public static string ToDisplayString<T>(
            this IEnumerable<T> collection,
            Func<T, object> selector = null,
            bool detailedFormat = true)
        {
            if (collection == null)
                return "null";

            var items = collection as IList<T> ?? new List<T>(collection);

            if (!detailedFormat)
                return FormatSimpleCollection(items, selector);

            var builder = new StringBuilder();
            Type collectionType = collection.GetType();
            builder.Append(collectionType.FullName ?? collectionType.Name);
            builder.Append(", Count: ");
            builder.Append(items.Count.ToString(CultureInfo.InvariantCulture));

            if (items.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("=======");

                for (int index = 0; index < items.Count; index++)
                {
                    builder.Append('[');
                    builder.Append(index.ToString(CultureInfo.InvariantCulture));
                    builder.Append("] ");
                    builder.AppendLine(FormatItem(items[index], selector));
                }

                builder.Append("=======");
            }

            return builder.ToString();
        }

        private static string FormatSimpleCollection<T>(IList<T> items, Func<T, object> selector)
        {
            var builder = new StringBuilder();
            builder.Append('[');

            for (int index = 0; index < items.Count; index++)
            {
                if (index > 0)
                    builder.Append(", ");

                builder.Append(FormatItem(items[index], selector));
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static string FormatItem<T>(T item, Func<T, object> selector)
        {
            object value = selector == null ? item : selector(item);
            if (value == null)
                return "null";

            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString();
        }
    }
}
