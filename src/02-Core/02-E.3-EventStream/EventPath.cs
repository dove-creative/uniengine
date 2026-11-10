using System;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public class EventPath<T> : IRegistrable<T>
    {
        // Internal
        internal int Depth { get; }

        private readonly IRegistrable<T> _source;
        private readonly PathInfo<T> _info;


        // Content --------------------
        // Construction
        internal EventPath(IRegistrable<T> source, PathInfo<T> info)
        {
            _source = source;
            _info = info;
            Depth = source.Depth + 1;
            EventStream<T>.CheckDepth(Depth);
        }

        // Operators
        public EventPath<T> Where(Func<T, bool> predicate) =>
            new(this, PathInfo<T>.Where(predicate));
        public EventPath<T> Debug(string message = "Debug", LogLevel logType = LogLevel.Log) =>
            new(this, PathInfo<T>.Debug(message, logType));
        public EventPath<T> ForOrder<TOrder>(TOrder order) where TOrder : Enum =>
            new(this, PathInfo<T>.Group(Convert.ToInt32(order)));

        // Subscriptions
        public IDisposable Subscribe(Action<T> updated) =>
            Register(Registration<T>.Subscription(updated, null, null));
        public IDisposable Subscribe(Action<T> updated, Action completed, Action<Exception> errored) =>
            Register(Registration<T>.Subscription(updated, completed, errored));
        public IDisposable Subscribe(IObserver<T> observer) =>
            Register(Registration<T>.Subscription(observer));

        // Relations
        public EventStream<T> Take(int count = 1) =>
            new TakeStream<T>(EventStream<T>.CreateInput(this), count);
        public EventStream<T> TakeWhile(Func<T, bool> predicate) =>
            new TakeWhileStream<T>(EventStream<T>.CreateInput(this), predicate);
        public EventStream<T> Skip(int count = 1) =>
            new SkipStream<T>(EventStream<T>.CreateInput(this), count);
        public EventStream<T> SkipWhile(Func<T, bool> predicate) =>
            new SkipWhileStream<T>(EventStream<T>.CreateInput(this), predicate);
        public EventStream<T> Branch() =>
            new(EventStream<T>.CreateInput(this));

        // Registration
        int IRegistrable<T>.Depth => Depth;
        bool IRegistrable<T>.IsDisposed => _source.IsDisposed;

        void IRegistrable<T>.ThrowIfDisposed() => _source.ThrowIfDisposed();

        // A path does not register anything itself. It links its own step in front of the steps after it.
        IDisposable IRegistrable<T>.Register(Registration<T> registration, PathInfo<T> path) =>
            _source.Register(registration, _info.Link(path));

        private IDisposable Register(Registration<T> registration) =>
            ((IRegistrable<T>)this).Register(registration, null);
    }

    // Stream entry points and type-specific paths stay here, so EventStream does not depend on EventPath.
    public static class EventPathExtensions
    {
        public static EventPath<T> Where<T>(this EventStream<T> stream, Func<T, bool> predicate) =>
            new(stream, PathInfo<T>.Where(predicate));
        public static EventPath<T> Debug<T>(
            this EventStream<T> stream,
            string message = "Debug",
            LogLevel logType = LogLevel.Log) =>
            new(stream, PathInfo<T>.Debug(message, logType));
        public static EventPath<T> ForOrder<T, TOrder>(this EventStream<T> stream, TOrder order) where TOrder : Enum =>
            new(stream, PathInfo<T>.Group(Convert.ToInt32(order)));
        public static EventPath<ulong> ForFlags<TFlags>(this EventStream<ulong> stream, TFlags mask)
            where TFlags : struct, Enum =>
            new(stream, PathInfo<ulong>.Flags(GetMask(mask)));
        public static EventPath<ulong> ForFlags<TFlags>(this EventPath<ulong> path, TFlags mask)
            where TFlags : struct, Enum =>
            new(path, PathInfo<ulong>.Flags(GetMask(mask)));

        private static ulong GetMask<TFlags>(TFlags mask) where TFlags : struct, Enum
        {
            if (!FlagType<TFlags>.IsFlags)
                throw new ArgumentException("The mask type must have the Flags attribute.", nameof(mask));

            var bits = mask.ToBits();
            if (bits == 0)
                throw new ArgumentException("The mask must contain at least one bit.", nameof(mask));

            return bits;
        }

        private static class FlagType<TFlags> where TFlags : struct, Enum
        {
            internal static readonly bool IsFlags = typeof(TFlags).IsDefined(typeof(FlagsAttribute), false);
        }
    }
}
