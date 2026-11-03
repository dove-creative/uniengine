using System;
using System.Collections.Generic;
using System.Linq;
using Blackthunder.UniEngine.Core.EventStreamCore;

namespace Blackthunder.UniEngine.Core
{
    public interface IEventRouter<TEventArgs> : IEventStream<TEventArgs>
    {
        IDisposable Subscribe(TEventArgs target, Action<TEventArgs> updated);
        IDisposable Subscribe(IEnumerable<TEventArgs> targets, Action<TEventArgs> updated);
        IDisposable Subscribe(TEventArgs target, Action<TEventArgs> updated, Action completed, Action<Exception> errored);
        IDisposable Subscribe(IEnumerable<TEventArgs> targets, Action<TEventArgs> updated, Action completed, Action<Exception> errored);
        IDisposable Subscribe(TEventArgs target, IObserver<TEventArgs> observer);
        IDisposable Subscribe(IEnumerable<TEventArgs> targets, IObserver<TEventArgs> observer);
    }

    internal class EventRouter<TEventArgs> : IEventRouter<TEventArgs>, IPassiveObserver
    {
        // Front
        public bool Enabled
        {
            get => _stream.Enabled;
            set => _stream.Enabled = value;
        }

        public TerminationMode TerminationMode
        {
            get => _stream.TerminationMode;
            set => _stream.TerminationMode = value;
        }

        public bool IsDisposed => _stream.IsDisposed;


        // Internal
        // The wrapped stream owns subscriptions, connections, failures, and termination.
        private readonly EventStream<TEventArgs> _stream = new();
        private readonly Func<TEventArgs, uint> _toMask;
        // Targeted callbacks compare their own mask with the mask of the value being published.
        private uint _mask;


        // Content --------------------
        // Construction
        public EventRouter() { }
        public EventRouter(Func<TEventArgs, uint> toMask) => _toMask = toMask;

        // Propagation
        public void OnNext(TEventArgs args)
        {
            ThrowIfDisposed();
            if (!Enabled) return;

            var mask = ToMask(args);
            if (IsDisposed) return;

            // A rejected nested publication must not change the mask seen by the remaining callbacks.
            var outerMask = _mask;
            _mask = mask;
            try
            {
                _stream.OnNext(args);
            }
            finally
            {
                _mask = outerMask;
            }
        }

        public void OnCompleted() => _stream.OnCompleted();
        public void OnError(Exception exception) => _stream.OnError(exception);

        public IEventStream<TEventArgs> With(TerminationMode terminationMode)
        {
            TerminationMode = terminationMode;
            return this;
        }

        // Subscriptions
        public IDisposable Subscribe(Action<TEventArgs> updated) => _stream.Subscribe(updated);
        public IDisposable Subscribe(Action<TEventArgs> updated, Action completed, Action<Exception> errored) =>
            _stream.Subscribe(updated, completed, errored);
        public IDisposable Subscribe(IObserver<TEventArgs> observer) => _stream.Subscribe(observer);

        public IDisposable Subscribe(TEventArgs target, Action<TEventArgs> updated) =>
            Subscribe(target, updated, null, null);
        public IDisposable Subscribe(IEnumerable<TEventArgs> targets, Action<TEventArgs> updated) =>
            Subscribe(targets, updated, null, null);

        public IDisposable Subscribe(TEventArgs target, Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            ThrowIfDisposed();
            if (updated == null) return _stream.Subscribe(null, completed, errored);

            return _stream.Subscribe(CreateRoutedCallback(ToMask(target), updated), completed, errored);
        }

        public IDisposable Subscribe(IEnumerable<TEventArgs> targets, Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            ThrowIfDisposed();
            if (updated == null) return _stream.Subscribe(null, completed, errored);

            return _stream.Subscribe(CreateRoutedCallback(CombineMasks(targets), updated), completed, errored);
        }

        public IDisposable Subscribe(TEventArgs target, IObserver<TEventArgs> observer)
        {
            ThrowIfDisposed();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            return _stream.Subscribe(
                CreateRoutedCallback(ToMask(target), observer.OnNext), observer.OnCompleted, observer.OnError);
        }

        public IDisposable Subscribe(IEnumerable<TEventArgs> targets, IObserver<TEventArgs> observer)
        {
            ThrowIfDisposed();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            return _stream.Subscribe(
                CreateRoutedCallback(CombineMasks(targets), observer.OnNext), observer.OnCompleted, observer.OnError);
        }

        private Action<TEventArgs> CreateRoutedCallback(uint mask, Action<TEventArgs> updated)
        {
            // Wrap each entry so the stream still stops a multicast tail after its subscription is released.
            Action<TEventArgs> routed = null;
            foreach (var entry in updated.GetInvocationList())
            {
                var callback = (Action<TEventArgs>)entry;
                routed += args =>
                {
                    if (_mask.HasAny(mask)) callback(args);
                };
            }

            return routed;
        }

        private uint ToMask(TEventArgs args) => _toMask?.Invoke(args) ?? uint.MaxValue;
        private uint CombineMasks(IEnumerable<TEventArgs> targets) => FlagExtensions.Combine(targets.Select(ToMask));

        // Connections
        void IPassiveObserver.TakeConnection(IDisposable connection) =>
            ((IPassiveObserver)_stream).TakeConnection(connection);

        public TObserver Chain<TObserver>(TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            ThrowIfDisposed();
            if (ReferenceEquals(observer, this))
                throw new ArgumentException("A stream cannot chain itself.", nameof(observer));

            return _stream.Chain(observer, out connection);
        }

        public IEventStream<TEventArgs> Connect<TObserver>(TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            Chain(observer, out connection);
            return this;
        }

        // Disposal
        private void ThrowIfDisposed() => EventStream<TEventArgs>.ThrowIfDisposed(_stream);

        public void Dispose() => _stream.Dispose();
    }
}
