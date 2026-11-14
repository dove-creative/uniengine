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
        public EventPath<T> When(T target) =>
            new(this, PathInfo<T>.When(target));
        public EventPath<T> Debug(string message = "Debug", LogLevel logType = LogLevel.Log) =>
            new(this, PathInfo<T>.Debug(message, logType));
        public EventPath<T> ForOrder<TOrder>(TOrder order) where TOrder : Enum =>
            new(this, PathInfo<T>.Group(Convert.ToInt32(order)));

        public EventPath<T> ForOrderAsync<TOrder>(TOrder order) where TOrder : Enum =>
            new(this, PathInfo<T>.ParallelGroup(Convert.ToInt32(order)));

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

    // Flag paths exist only for ulong streams and paths, so they stay extensions instead of instance methods.
    public static class EventPathExtensions
    {
        public static EventPath<ulong> ForFlags<TFlags>(this EventStream<ulong> stream, TFlags mask)
            where TFlags : struct, Enum =>
            new(stream, PathInfo<ulong>.Flags(mask));
        public static EventPath<ulong> ForFlags<TFlags>(this EventPath<ulong> path, TFlags mask)
            where TFlags : struct, Enum =>
            new(path, PathInfo<ulong>.Flags(mask));
    }
}
