using System;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    internal enum PathOperation { Group, Where, Debug }

    public sealed class EventPath<T>
    {
        // Internal
        private readonly EventStream<T> _stream;
        internal PathOperation Operation { get; }
        internal EventPath<T> Parent { get; }
        internal Filter<T> Filter { get; }
        internal int Order { get; }
        internal int Depth { get; }
        internal string Message { get; }
        internal LogLevel LogType { get; }


        // Content --------------------
        // Construction
        internal EventPath(EventStream<T> stream, EventPath<T> parent, PathOperation operation,
            int order = 0, Filter<T> filter = null, string message = null, LogLevel logType = LogLevel.Log)
        {
            _stream = stream;
            Parent = parent;
            Depth = (parent?.Depth ?? stream.UpstreamDepth) + 1;
            EventStream<T>.CheckDepth(Depth);
            Operation = operation;
            Order = order;
            if (operation == PathOperation.Where && filter == null)
                throw new ArgumentNullException(nameof(filter));
            Filter = filter;
            Message = message;
            LogType = logType;
        }

        // Operators
        public EventPath<T> Where(Filter<T> filter) => new(_stream, this, PathOperation.Where, filter: filter);
        public EventPath<T> Debug(string message = "Debug", LogLevel logType = LogLevel.Log) =>
            new(_stream, this, PathOperation.Debug, message: message, logType: logType);

        // Subscriptions
        public IDisposable Subscribe(Action<T> updated) => _stream.Subscribe(this, updated, null, null);
        public IDisposable Subscribe(Action<T> updated, Action completed, Action<Exception> errored) =>
            _stream.Subscribe(this, updated, completed, errored);
        public IDisposable Subscribe(IObserver<T> observer) => _stream.Subscribe(this, observer);

        // Relations
        public EventStream<T> Take(int count = 1) => new TakeStream<T>(_stream.CreateUpstream(this), count);
        public EventStream<T> TakeWhile(Filter<T> filter) => new TakeWhileStream<T>(_stream.CreateUpstream(this), filter);
        public EventStream<T> Skip(int count = 1) => new SkipStream<T>(_stream.CreateUpstream(this), count);
        public EventStream<T> SkipWhile(Filter<T> filter) => new SkipWhileStream<T>(_stream.CreateUpstream(this), filter);
        public EventStream<T> Branch() => new(_stream.CreateUpstream(this));
    }
}
