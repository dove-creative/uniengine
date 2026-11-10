using System;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    internal enum PathNodeType { Group, Where, Debug, Flags }

    // One step of a path. Every registration links new copies from the root side to its end,
    // so a chain is used by one registration only and nothing in it is shared or changed later.
    internal class PathInfo<T>
    {
        // Front
        internal PathNodeType NodeType { get; }
        internal Func<T, bool> Predicate { get; }
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
            int order,
            ulong mask,
            string message,
            LogLevel logType,
            PathInfo<T> next)
        {
            NodeType = nodeType;
            Predicate = predicate;
            Order = order;
            Mask = mask;
            Message = message;
            LogType = logType;
            Next = next;
        }

        internal static PathInfo<T> Where(Func<T, bool> predicate) => new(
            PathNodeType.Where,
            predicate ?? throw new ArgumentNullException(nameof(predicate)),
            0,
            0,
            null,
            LogLevel.Log,
            null);
        internal static PathInfo<T> Debug(string message, LogLevel logType) =>
            new(PathNodeType.Debug, null, 0, 0, message, logType, null);
        internal static PathInfo<T> Group(int order) =>
            new(PathNodeType.Group, null, order, 0, null, LogLevel.Log, null);
        internal static PathInfo<T> Flags(ulong mask) =>
            new(PathNodeType.Flags, null, 0, mask, null, LogLevel.Log, null);

        // Chain
        internal PathInfo<T> Link(PathInfo<T> next) =>
            new(NodeType, Predicate, Order, Mask, Message, LogType, next);
    }
}
