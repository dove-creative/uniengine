using System;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    internal enum PathNodeType { Group, ParallelGroup, Where, When, Debug, Flags }

    // One step of a path. Every registration links new copies from the root side to its end,
    // so a chain is used by one registration only and nothing in it is shared or changed later.
    internal class PathInfo<T>
    {
        // Front
        internal PathNodeType NodeType { get; }
        internal Func<T, bool> Predicate { get; }
        internal T Target { get; }
        internal int Order { get; }
        internal ulong Mask { get; }
        internal string Message { get; }
        internal LogLevel LogType { get; }
        internal PathInfo<T> Next { get; }


        // Content --------------------
        // Construction
        private PathInfo(
            PathNodeType nodeType,
            Func<T, bool> predicate,
            T target,
            int order,
            ulong mask,
            string message,
            LogLevel logType,
            PathInfo<T> next)
        {
            NodeType = nodeType;
            Predicate = predicate;
            Target = target;
            Order = order;
            Mask = mask;
            Message = message;
            LogType = logType;
            Next = next;
        }

        internal static PathInfo<T> Where(Func<T, bool> predicate) => new(
            PathNodeType.Where,
            predicate ?? throw new ArgumentNullException(nameof(predicate)),
            default,
            0,
            0,
            null,
            LogLevel.Log,
            null);
        internal static PathInfo<T> When(T target) =>
            new(PathNodeType.When, null, target, 0, 0, null, LogLevel.Log, null);
        internal static PathInfo<T> Debug(string message, LogLevel logType) =>
            new(PathNodeType.Debug, null, default, 0, 0, message, logType, null);
        internal static PathInfo<T> Group(int order) =>
            new(PathNodeType.Group, null, default, order, 0, null, LogLevel.Log, null);

        internal static PathInfo<T> ParallelGroup(int order) =>
            new(PathNodeType.ParallelGroup, null, default, order, 0, null, LogLevel.Log, null);

        internal static PathInfo<T> Flags<TFlags>(TFlags mask) where TFlags : struct, Enum
        {
            if (!FlagType<TFlags>.IsFlags)
                throw new ArgumentException("The mask type must have the Flags attribute.", nameof(mask));

            var bits = mask.ToBits();
            if (bits == 0)
                throw new ArgumentException("The mask must contain at least one bit.", nameof(mask));

            return new(PathNodeType.Flags, null, default, 0, bits, null, LogLevel.Log, null);
        }

        // Chain
        internal PathInfo<T> Link(PathInfo<T> next) =>
            new(NodeType, Predicate, Target, Order, Mask, Message, LogType, next);

        // Caches the attribute check per enum type, since paths are built on every call.
        private static class FlagType<TFlags> where TFlags : struct, Enum
        {
            internal static readonly bool IsFlags = typeof(TFlags).IsDefined(typeof(FlagsAttribute), false);
        }
    }
}
