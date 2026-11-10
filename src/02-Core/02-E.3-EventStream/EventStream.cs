using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public partial class EventStream<T> : IObservable<T>, IObserver<T>, IDisposable, IRegistrable<T>
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

        private const int MaximumDepth = 256;
        private readonly EventGraph<T> _graph;
        private readonly bool _isRelation;
        private InputGate[] _inputs;
        private int _openInputs;
        private bool _isPending;
        private bool _isEnabled = true;
        private bool _isDisposed;
        private bool _isTerminating;


        // Content --------------------
        // Construction
        public EventStream() => _graph = new EventGraph<T>(null);
        public EventStream(IObservable<T> source) : this()
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var input = new InputGate();
            input.Bind(this);
            _inputs = new[] { input };
            _openInputs = 1;

            try
            {
                input.Connect(source);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        // A relation connects to its inputs with its first subscription and ends with its last one.
        internal EventStream(params InputGate[] inputs)
        {
            foreach (var input in inputs)
            {
                input.Bind(this);
                UpstreamDepth = Math.Max(UpstreamDepth, input.Depth + 1);
            }

            CheckDepth(UpstreamDepth);
            _isRelation = true;
            _inputs = inputs;
            _isPending = true;
            _graph = new EventGraph<T>(Dispose);
        }

        internal void Inject(EventGraph<T>.Node root) => _graph.Inject(root);

        private void PrepareSubscription()
        {
            ThrowIfDisposed();

            if (!_isPending) return;
            _isPending = false;

            var inputs = _inputs;
            _openInputs = inputs.Length;

            try
            {
                for (int i = 0; i < inputs.Length && !_isDisposed; i++)
                {
                    // An upstream can end before a pending relation connects to it.
                    if (inputs[i].IsUpstreamDisposed)
                    {
                        _openInputs--;
                        continue;
                    }

                    inputs[i].Connect();
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
            if (!_isPending) return false;

            foreach (var input in _inputs)
            {
                if (!input.IsUpstreamDisposed)
                    return false;
            }

            return true;
        }

        // Propagation
        public void OnNext(T value)
        {
            if (_graph.IsPublishing || (_isTerminating && !IsDisposed))
                throw new InvalidOperationException("A stream cannot publish another value during propagation.");

            ThrowIfDisposed();

            if (_isEnabled) _graph.OnNext(value);
        }
        public void OnCompleted() => Terminate(null, true);
        public void OnError(Exception exception) => Terminate(exception, false);

        // A failed condition releases the relation without a terminal notification.
        internal bool TryEvaluate(Func<T, bool> predicate, T value, out bool matches)
        {
            try
            {
                matches = predicate(value);
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
            if (--_openInputs <= 0)
                Terminate(null, true);
        }

        private void LoseInput()
        {
            if (--_openInputs <= 0 && _isRelation)
                Dispose();
        }

        // Paths and subscriptions
        internal static InputGate CreateInput(IRegistrable<T> upstream)
        {
            upstream.ThrowIfDisposed();
            return new InputGate(upstream);
        }

        internal static void CheckDepth(int depth)
        {
            if (depth > MaximumDepth)
                throw new InvalidOperationException($"An event path cannot exceed {MaximumDepth} steps.");
        }

        public IDisposable Subscribe(Action<T> updated) =>
            Register(Registration<T>.Subscription(updated, null, null), null);
        public IDisposable Subscribe(Action<T> updated, Action completed, Action<Exception> errored) =>
            Register(Registration<T>.Subscription(updated, completed, errored), null);
        public IDisposable Subscribe(IObserver<T> observer) =>
            Register(Registration<T>.Subscription(observer), null);

        // Registration
        int IRegistrable<T>.Depth => UpstreamDepth;

        void IRegistrable<T>.ThrowIfDisposed() => ThrowIfDisposed();
        IDisposable IRegistrable<T>.Register(Registration<T> registration, PathInfo<T> path) =>
            Register(registration, path);

        // A path arrives as a chain from the root side, so the graph only needs node operations.
        private IDisposable Register(Registration<T> registration, PathInfo<T> path)
        {
            PrepareSubscription();

            var node = _graph.BeginRegistration();
            for (var info = path; info != null; info = info.Next)
            {
                node = info.NodeType switch
                {
                    PathNodeType.Group => _graph.FindOrAddGroup(node, info.Order),
                    PathNodeType.Where => _graph.FindOrAddWhere(node, info.Predicate),
                    PathNodeType.Flags => _graph.FindOrAddFlags(node, info.Mask),
                    _ => _graph.AddDebug(node, info.Message, info.LogType)
                };
            }

            return registration.IsConnection
                ? _graph.Connect(node, registration.Input)
                : _graph.Subscribe(
                    node,
                    registration.Updated,
                    registration.Completed,
                    registration.Errored,
                    registration.Input);
        }

        // Relations
        public EventStream<T> Take(int count = 1) =>
            new TakeStream<T>(CreateInput(this), count);
        public EventStream<T> TakeWhile(Func<T, bool> predicate) =>
            new TakeWhileStream<T>(CreateInput(this), predicate);
        public EventStream<T> Skip(int count = 1) =>
            new SkipStream<T>(CreateInput(this), count);
        public EventStream<T> SkipWhile(Func<T, bool> predicate) =>
            new SkipWhileStream<T>(CreateInput(this), predicate);
        public EventStream<T> Branch() =>
            new(CreateInput(this));

        public EventStream<T> Merge(params EventStream<T>[] others)
        {
            var inputs = new List<InputGate> { CreateInput(this) };
            if (others != null)
            {
                foreach (var other in others)
                {
                    if (other != null) inputs.Add(CreateInput(other));
                }
            }

            return new EventStream<T>(inputs.ToArray());
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
            _isPending = false;
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
    }
}
