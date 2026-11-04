using System;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public class EventStream<T> : IObservable<T>, IDisposable
    {
        // Front
        public bool Enabled
        {
            get => !IsDisposed && ((_source as IEventSource<T>)?.Enabled ?? (_source as EventStream<T>)?.Enabled ?? _branchEnabled);
            set
            {
                RequireOwnership();
                if (value) ThrowIfDisposed();
                if (_ownedSource != null) _ownedSource.Enabled = value;
                else _branchEnabled = value;
            }
        }
        public bool IsDisposed => _disposed || (_pendingParent?.IsDisposed ?? false);

        // Internal
        internal int NodeCount => _graph.NodeCount;
        internal int UpstreamDepth { get; }
        internal virtual EventPath<T> DefaultPath => null;

        private const int MaximumDepth = 256;
        private readonly EventGraph<T> _graph;
        private readonly bool _ownsSource;
        private EventSource<T> _ownedSource;
        private IObservable<T> _source;
        private IDisposable _connection;
        private Handle _parentConnection;
        private EventStream<T> _pendingParent;
        private EventPath<T> _pendingPath;
        private bool _branchEnabled = true;
        private bool _disposed;
        private bool _isTerminating;


        // Content --------------------
        // Construction
        public EventStream()
            : this(new EventSource<T>(), true) { }

        public EventStream(IObservable<T> source)
            : this(source, false) { }

        private EventStream(IObservable<T> source, bool owned)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            _ownsSource = owned;
            _ownedSource = owned ? (EventSource<T>)source : null;
            _graph = new EventGraph<T>(null);
            if (_ownedSource?.IsDisposed == true) Dispose();
            else Connect(source);
        }

        private EventStream(EventStream<T> parent, EventPath<T> path)
        {
            UpstreamDepth = (path?.Depth ?? parent.UpstreamDepth) + 1;
            CheckDepth(UpstreamDepth);
            _pendingParent = parent;
            _pendingPath = path;
            _ownsSource = true;
            _graph = new EventGraph<T>(Dispose);
        }

        public static EventStream<T> Merge(params IEventSource<T>[] upstreams) =>
            new(new EventStreamCore.Merge<T>(EventStreamCore.Merge<T>.CompletionMode.All, upstreams), true);

        private void Connect(IObservable<T> source)
        {
            _source = source;
            try
            {
                var connection = source.Subscribe(new SourceObserver(this));
                // An observable may terminate before returning its connection.
                if (IsDisposed)
                    connection?.Dispose();
                else
                    _connection = connection;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        // Propagation
        public void OnNext(T value)
        {
            RequireOwnership();
            if (_graph.IsPublishing || (_isTerminating && !IsDisposed))
                throw new InvalidOperationException("A stream cannot publish another value during propagation.");
            ThrowIfDisposed();
            _ownedSource?.OnNext(value);
        }
        public void OnCompleted()
        {
            RequireOwnership();
            if (_ownedSource != null) _ownedSource.OnCompleted();
            else Terminate(null, true);
        }
        public void OnError(Exception exception)
        {
            RequireOwnership();
            if (_ownedSource != null) _ownedSource.OnError(exception);
            else Terminate(exception, false);
        }

        internal void CompleteFromParent() => Terminate(null, true);
        internal void ErrorFromParent(Exception error) => Terminate(error, false);

        private void RequireOwnership()
        {
            if (!_ownsSource) throw new InvalidOperationException("The stream does not own its source.");
        }

        private void Terminate(Exception error, bool completed)
        {
            if (IsDisposed || _isTerminating) return;
            _isTerminating = true;
            try
            {
                _graph.Terminate(error, completed);
            }
            finally
            {
                Dispose();
            }
        }

        // Paths and subscriptions
        internal EventPath<T> CreateGroupPath(int order) => new(this, null, PathOperation.Group, order);

        internal static void CheckDepth(int depth)
        {
            if (depth > MaximumDepth)
                throw new InvalidOperationException($"An event path cannot exceed {MaximumDepth} steps.");
        }

        public EventPath<T> Where(Filter<T> filter) => new(this, DefaultPath, PathOperation.Where, filter: filter);
        public EventPath<T> Take(int count = 1) => new(this, DefaultPath, PathOperation.Take, count);
        public EventPath<T> Skip(int count = 1) => new(this, DefaultPath, PathOperation.Skip, count);
        public EventPath<T> SkipWhile(Filter<T> filter) => new(this, DefaultPath, PathOperation.SkipWhile, filter: filter);
        public EventPath<T> TakeWhile(Filter<T> filter) => new(this, DefaultPath, PathOperation.TakeWhile, filter: filter);
        public EventPath<T> Debug(string message = "Debug", LogLevel logType = LogLevel.Log) =>
            new(this, DefaultPath, PathOperation.Debug, message: message, logType: logType);

        public IDisposable Subscribe(Action<T> updated) => Subscribe(DefaultPath, updated, null, null);
        public IDisposable Subscribe(Action<T> updated, Action completed, Action<Exception> errored) =>
            Subscribe(DefaultPath, updated, completed, errored);
        public IDisposable Subscribe(IObserver<T> observer) => Subscribe(DefaultPath, observer);

        internal IDisposable Subscribe(EventPath<T> path, IObserver<T> observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            PrepareSubscription();
            return _graph.Subscribe(path, observer.OnNext, observer.OnCompleted, observer.OnError, observer is IGraphObserver<T>);
        }
        internal IDisposable Subscribe(EventPath<T> path, Action<T> updated, Action completed, Action<Exception> errored)
        {
            PrepareSubscription();
            return _graph.Subscribe(path, updated, completed, errored);
        }

        public EventStream<T> Branch() => Branch(DefaultPath);
        internal EventStream<T> Branch(EventPath<T> path)
        {
            ThrowIfDisposed();
            return new EventStream<T>(this, path);
        }

        private void PrepareSubscription()
        {
            ThrowIfDisposed();
            if (_pendingParent == null) return;

            _pendingParent.PrepareSubscription();
            _ownedSource = new EventSource<T> { Enabled = _branchEnabled };
            try
            {
                Connect(_ownedSource);
                _parentConnection = _pendingParent._graph.ConnectBranch(_pendingPath, this);
                _pendingParent = null;
                _pendingPath = null;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        // Disposal
        private void ThrowIfDisposed()
        {
            if (IsDisposed || _isTerminating)
                throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ExceptionDispatchInfo failure = null;
            DisposeConnection(_graph, ref failure);
            DisposeConnection(_parentConnection, ref failure);
            DisposeConnection(_connection, ref failure);
            DisposeConnection(_ownedSource, ref failure);
            _parentConnection = null;
            _connection = null;
            _source = null;
            _ownedSource = null;
            _pendingParent = null;
            _pendingPath = null;
            failure?.Throw();
        }

        private static void DisposeConnection(IDisposable connection, ref ExceptionDispatchInfo failure)
        {
            try
            {
                connection?.Dispose();
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        private sealed class SourceObserver : IGraphObserver<T>
        {
            private readonly EventStream<T> _stream;

            internal SourceObserver(EventStream<T> stream) => _stream = stream;

            public void OnNext(T value)
            {
                if (!_stream.IsDisposed && !_stream._isTerminating)
                    _stream._graph.OnNext(value);
            }
            public void OnCompleted() => _stream.Terminate(null, true);
            public void OnError(Exception error) => _stream.Terminate(error, false);
        }
    }
}
