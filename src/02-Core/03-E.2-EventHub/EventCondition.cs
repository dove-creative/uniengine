using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public static class EventCondition
    {
        public static EventCondition<T> Where<T>(Func<T, bool> predicate) =>
            EventCondition<T>.CreateWhere(predicate);
        public static EventCondition<T> When<T>(T target) =>
            EventCondition<T>.CreateWhen(target);
        public static EventCondition<ulong> Flags<TFlags>(TFlags mask) where TFlags : struct, Enum =>
            EventCondition<ulong>.CreateFlags(mask);
        public static EventCondition<ulong> Flags<TFlags>(this EventCondition<ulong> condition, TFlags mask)
            where TFlags : struct, Enum
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            return condition.AppendFlags(mask);
        }
    }

    /// <summary>
    /// An immutable sequence of update conditions, appended from the root toward the callback.
    /// </summary>
    public sealed class EventCondition<T>
    {
        // Internal
        internal EventCondition<T> Previous { get; }
        internal PathInfo<T> Step { get; }

        private readonly int _depth;
        private readonly Delegate _predicateSource;


        // Content --------------------
        // Construction
        private EventCondition(
            EventCondition<T> previous,
            PathInfo<T> step,
            Delegate predicateSource = null)
        {
            _depth = (previous?._depth ?? 0) + 1;
            EventStream<T>.CheckDepth(_depth);

            Previous = previous;
            Step = step;
            _predicateSource = predicateSource;
        }

        internal static EventCondition<T> CreateWhere(Func<T, bool> predicate) =>
            new(null, PathInfo<T>.Where(predicate), predicate);
        internal static EventCondition<T> CreateWhen(T target) =>
            new(null, PathInfo<T>.When(target));
        internal static EventCondition<T> CreateFlags<TFlags>(TFlags mask) where TFlags : struct, Enum =>
            new(null, PathInfo<T>.Flags(mask));

        internal static EventCondition<T> CreateObjectWhere(Func<object, bool> condition)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            // Compare the original delegate, while the stream evaluates the typed adapter.
            return new EventCondition<T>(null, PathInfo<T>.Where(value => condition(value)), condition);
        }

        // Steps
        public EventCondition<T> Where(Func<T, bool> predicate) =>
            new(this, PathInfo<T>.Where(predicate), predicate);
        public EventCondition<T> When(T target) =>
            new(this, PathInfo<T>.When(target));
        public EventCondition<T> Debug(string message = "Debug", LogLevel logType = LogLevel.Log) =>
            new(this, PathInfo<T>.Debug(message, logType));

        internal EventCondition<T> AppendFlags<TFlags>(TFlags mask) where TFlags : struct, Enum =>
            new(this, PathInfo<T>.Flags(mask));

        // Registration identity is independent of the stream's node sharing rules.
        internal static bool Matches(EventCondition<T> first, EventCondition<T> second)
        {
            if (ReferenceEquals(first, second)) return true;
            if (first == null || second == null || first._depth != second._depth) return false;

            for (; first != null; first = first.Previous, second = second.Previous)
            {
                if (first.Step.NodeType != second.Step.NodeType)
                    return false;

                var equal = first.Step.NodeType switch
                {
                    PathNodeType.Where => Equals(first._predicateSource, second._predicateSource),
                    PathNodeType.When => EqualityComparer<T>.Default.Equals(first.Step.Target, second.Step.Target),
                    PathNodeType.Flags => first.Step.Mask == second.Step.Mask,
                    PathNodeType.Debug => first.Step.Message == second.Step.Message
                        && first.Step.LogType == second.Step.LogType,
                    _ => false
                };

                if (!equal)
                    return false;
            }

            return true;
        }
    }
}
