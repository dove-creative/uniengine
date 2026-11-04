using System;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    internal enum PathOperation { Group, Where, Take, Skip, SkipWhile, TakeWhile, Debug }

    public sealed class EventPath<T>
    {
        // Internal
        private readonly EventStream<T> _stream;
        internal PathOperation Operation { get; }
        internal EventPath<T> Parent { get; }
        internal Filter<T> Filter { get; }
        internal int Count { get; }
        internal int Depth { get; }
        internal string Message { get; }
        internal LogLevel LogType { get; }


        // Content --------------------
        // Construction
        internal EventPath(EventStream<T> stream, EventPath<T> parent, PathOperation operation,
            int count = 0, Filter<T> filter = null, string message = null, LogLevel logType = LogLevel.Log)
        {
            _stream = stream;
            Parent = parent;
            Depth = (parent?.Depth ?? stream.UpstreamDepth) + 1;
            EventStream<T>.CheckDepth(Depth);
            Operation = operation;
            Count = count;
            if ((operation == PathOperation.Where || operation == PathOperation.SkipWhile || operation == PathOperation.TakeWhile)
                && filter == null)
                throw new ArgumentNullException(nameof(filter));
            Filter = filter;
            Message = message;
            LogType = logType;
        }

        // Operators
        public EventPath<T> Where(Filter<T> filter) => new(_stream, this, PathOperation.Where, filter: filter);
        public EventPath<T> Take(int count = 1) => new(_stream, this, PathOperation.Take, count);
        public EventPath<T> Skip(int count = 1) => new(_stream, this, PathOperation.Skip, count);
        public EventPath<T> SkipWhile(Filter<T> filter) => new(_stream, this, PathOperation.SkipWhile, filter: filter);
        public EventPath<T> TakeWhile(Filter<T> filter) => new(_stream, this, PathOperation.TakeWhile, filter: filter);
        public EventPath<T> Debug(string message = "Debug", LogLevel logType = LogLevel.Log) =>
            new(_stream, this, PathOperation.Debug, message: message, logType: logType);

        // Subscriptions
        public IDisposable Subscribe(Action<T> updated) => _stream.Subscribe(this, updated, null, null);
        public IDisposable Subscribe(Action<T> updated, Action completed, Action<Exception> errored) =>
            _stream.Subscribe(this, updated, completed, errored);
        public IDisposable Subscribe(IObserver<T> observer) => _stream.Subscribe(this, observer);

        public EventStream<T> Branch() => _stream.Branch(this);
    }
}
