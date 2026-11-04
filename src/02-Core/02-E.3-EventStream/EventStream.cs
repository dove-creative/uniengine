using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public class EventStream<T> : IObservable<T>, IObserver<T>, IDisposable
    {
        // Front
        public bool Enabled
        {
            get => !IsDisposed && _isEnabled;
            set
            {
                if (value) ThrowIfDisposed();
                _isEnabled = value;
            }
        }
        public bool IsDisposed => _isDisposed || HaveUpstreamsEnded();

        // Internal
        internal int NodeCount => _graph.NodeCount;
        internal int UpstreamDepth { get; }
        internal virtual EventPath<T> DefaultPath => null;

        private const int MaximumDepth = 256;
        private readonly EventGraph<T> _graph;
        private readonly bool _isRelation;
        private Upstream<T>[] _pendingUpstreams;
        private Input[] _inputs;
        private int _openInputs;
        private bool _isEnabled = true;
        private bool _isDisposed;
        private bool _isTerminating;


        // Content --------------------
        // Construction
        public EventStream() => _graph = new EventGraph<T>(null);

        public EventStream(IObservable<T> source)
            : this()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var input = new Input(this);
            _inputs = new[] { input };
            _openInputs = 1;
            try
            {
                input.Keep(source.Subscribe(input));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        // A relation connects to its inputs with its first subscription and ends with its last one.
        internal EventStream(params Upstream<T>[] upstreams)
        {
            foreach (var upstream in upstreams)
                UpstreamDepth = Math.Max(UpstreamDepth, upstream.Depth + 1);

            CheckDepth(UpstreamDepth);
            _isRelation = true;
            _pendingUpstreams = upstreams;
            _graph = new EventGraph<T>(Dispose);
        }

        internal IDisposable ConnectInput(EventPath<T> path, IGraphObserver<T> input)
        {
            PrepareSubscription();
            return _graph.Connect(path, input);
        }

        private void PrepareSubscription()
        {
            ThrowIfDisposed();
            if (_pendingUpstreams == null) return;

            var upstreams = _pendingUpstreams;
            _pendingUpstreams = null;
            var inputs = new Input[upstreams.Length];
            _inputs = inputs;
            _openInputs = upstreams.Length;
            try
            {
                for (int i = 0; i < upstreams.Length && !_isDisposed; i++)
                {
                    // An upstream can end before a pending relation connects to it.
                    if (upstreams[i].IsDisposed)
                    {
                        _openInputs--;
                        continue;
                    }

                    inputs[i] = new Input(this);
                    inputs[i].Keep(upstreams[i].Connect(inputs[i]));
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool HaveUpstreamsEnded()
        {
            if (_pendingUpstreams == null) return false;

            foreach (var upstream in _pendingUpstreams)
            {
                if (!upstream.IsDisposed) return false;
            }
            return true;
        }

        // Propagation
        public void OnNext(T value)
        {
            if (_graph.IsPublishing || (_isTerminating && !IsDisposed))
                throw new InvalidOperationException("A stream cannot publish another value during propagation.");
            ThrowIfDisposed();
            if (_isEnabled) Publish(value);
        }
        public void OnCompleted() => Terminate(null, true);
        public void OnError(Exception exception) => Terminate(exception, false);

        internal virtual void Publish(T value) => _graph.OnNext(value);

        // A failed condition releases the relation without a terminal notification.
        internal bool TryEvaluate(Filter<T> filter, T value, out bool matches)
        {
            try
            {
                matches = filter.Predicate(value);
                return true;
            }
            catch (Exception exception)
            {
                matches = false;
                try
                {
                    Dispose();
                }
                finally
                {
                    ExceptionHandler.Report(exception);
                }
                return false;
            }
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

        private void CompleteInput()
        {
            if (--_openInputs <= 0) Terminate(null, true);
        }

        private void LoseInput()
        {
            if (--_openInputs <= 0 && _isRelation) Dispose();
        }

        // Paths and subscriptions
        internal EventPath<T> CreateGroupPath(int order) => new(this, null, PathOperation.Group, order);

        internal Upstream<T> CreateUpstream(EventPath<T> path)
        {
            ThrowIfDisposed();
            return new Upstream<T>(this, path);
        }

        internal static void CheckDepth(int depth)
        {
            if (depth > MaximumDepth)
                throw new InvalidOperationException($"An event path cannot exceed {MaximumDepth} steps.");
        }

        public EventPath<T> Where(Filter<T> filter) => new(this, DefaultPath, PathOperation.Where, filter: filter);
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
            return _graph.Subscribe(path, observer.OnNext, observer.OnCompleted, observer.OnError, observer as IGraphObserver<T>);
        }
        internal IDisposable Subscribe(EventPath<T> path, Action<T> updated, Action completed, Action<Exception> errored)
        {
            PrepareSubscription();
            return _graph.Subscribe(path, updated, completed, errored);
        }

        // Relations
        public EventStream<T> Take(int count = 1) => new TakeStream<T>(CreateUpstream(DefaultPath), count);
        public EventStream<T> TakeWhile(Filter<T> filter) => new TakeWhileStream<T>(CreateUpstream(DefaultPath), filter);
        public EventStream<T> Skip(int count = 1) => new SkipStream<T>(CreateUpstream(DefaultPath), count);
        public EventStream<T> SkipWhile(Filter<T> filter) => new SkipWhileStream<T>(CreateUpstream(DefaultPath), filter);
        public EventStream<T> Branch() => new(CreateUpstream(DefaultPath));

        public EventStream<T> Merge(params EventStream<T>[] others)
        {
            var upstreams = new List<Upstream<T>> { CreateUpstream(DefaultPath) };
            if (others != null)
            {
                foreach (var other in others)
                {
                    if (other != null) upstreams.Add(other.CreateUpstream(other.DefaultPath));
                }
            }
            return new EventStream<T>(upstreams.ToArray());
        }

        // Disposal
        private void ThrowIfDisposed()
        {
            if (IsDisposed || _isTerminating)
                throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            ExceptionDispatchInfo failure = null;
            DisposeConnection(_graph, ref failure);
            if (_inputs != null)
            {
                foreach (var input in _inputs)
                    DisposeConnection(input, ref failure);
            }
            _inputs = null;
            _pendingUpstreams = null;
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

        private sealed class Input : IGraphObserver<T>, IDisposable
        {
            private readonly EventStream<T> _stream;
            private IDisposable _connection;
            private bool _finished;

            internal Input(EventStream<T> stream) => _stream = stream;

            // A source can end before returning its connection.
            internal void Keep(IDisposable connection)
            {
                if (_finished) connection?.Dispose();
                else _connection = connection;
            }

            public void OnNext(T value)
            {
                if (!_finished && !_stream._isTerminating) _stream.OnNext(value);
            }

            public void OnCompleted()
            {
                if (_finished) return;
                _finished = true;
                try
                {
                    _stream.CompleteInput();
                }
                finally
                {
                    Release();
                }
            }

            public void OnError(Exception error)
            {
                if (_finished) return;
                _finished = true;
                try
                {
                    _stream.Terminate(error, false);
                }
                finally
                {
                    Release();
                }
            }

            public void OnDisconnected()
            {
                if (_finished) return;
                _finished = true;
                _connection = null;
                _stream.LoseInput();
            }

            public void Dispose()
            {
                _finished = true;
                Release();
            }

            private void Release()
            {
                var connection = _connection;
                _connection = null;
                connection?.Dispose();
            }
        }
    }
}
