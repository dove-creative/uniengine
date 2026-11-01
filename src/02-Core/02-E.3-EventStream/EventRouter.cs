using System;
using System.Collections.Generic;
using System.Linq;

namespace Blackthunder.UniEngine.Core
{
    public class EventRouter<TEventArgs> : EventStream<TEventArgs>
    {
        // Internal
        private readonly Func<TEventArgs, uint> _toMask;
        private readonly AdaptiveDictionary<Action<TEventArgs>, uint> _maskMap = new();


        // Content --------------------
        // Construction
        public EventRouter() { }
        public EventRouter(Func<TEventArgs, uint> toMask) => _toMask = toMask;

        // Propagation
        public override void OnNext(TEventArgs args)
        {
            ThrowIfDisposed();
            if (!Enabled) return;

            var mask = ToMask(args);
            if (IsDisposing) return;

            Dispatch(
                Updated,
                action => action(args),
                action => !_maskMap.TryGetValue(action, out var targetMask) || mask.HasAny(targetMask));
        }

        // Subscriptions
        public IDisposable Subscribe(TEventArgs target, Action<TEventArgs> updated)
        {
            ThrowIfDisposed();
            if (updated == null) return base.Subscribe(updated);

            return SubscribeRouted(ToMask(target), updated, null, null);
        }

        public IDisposable Subscribe(IEnumerable<TEventArgs> targets, Action<TEventArgs> updated)
        {
            ThrowIfDisposed();
            if (updated == null) return base.Subscribe(updated);

            return SubscribeRouted(FlagExtensions.Combine(targets.Select(ToMask)), updated, null, null);
        }

        public IDisposable Subscribe(TEventArgs target, Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            ThrowIfDisposed();
            if (updated == null) return base.Subscribe(updated, completed, errored);

            return SubscribeRouted(ToMask(target), updated, completed, errored);
        }

        public IDisposable Subscribe(IEnumerable<TEventArgs> targets, Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            ThrowIfDisposed();
            if (updated == null) return base.Subscribe(updated, completed, errored);

            return SubscribeRouted(FlagExtensions.Combine(targets.Select(ToMask)), updated, completed, errored);
        }

        public IDisposable Subscribe(TEventArgs target, IObserver<TEventArgs> observer)
        {
            ThrowIfDisposed();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            return SubscribeRouted(ToMask(target), observer.OnNext, observer.OnCompleted, observer.OnError);
        }

        public IDisposable Subscribe(IEnumerable<TEventArgs> targets, IObserver<TEventArgs> observer)
        {
            ThrowIfDisposed();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            return SubscribeRouted(
                FlagExtensions.Combine(targets.Select(ToMask)),
                observer.OnNext, observer.OnCompleted, observer.OnError);
        }

        private IDisposable SubscribeRouted(
            uint mask, Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            var handle = CreateSubscription(updated, completed, errored, out var registeredUpdated);
            _maskMap[registeredUpdated] = mask;
            Updated.Add(registeredUpdated);
            return handle;
        }

        // Connections
        public TObserver Chain<TObserver>(TEventArgs target, TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            ThrowIfDisposed();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));
            if (ReferenceEquals(observer, this))
                throw new ArgumentException("A stream cannot chain itself.", nameof(observer));

            connection = Subscribe(target, observer);
            BindConnection(observer, connection);
            return observer;
        }

        public TObserver Chain<TObserver>(IEnumerable<TEventArgs> targets, TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            ThrowIfDisposed();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));
            if (ReferenceEquals(observer, this))
                throw new ArgumentException("A stream cannot chain itself.", nameof(observer));

            connection = Subscribe(targets, observer);
            BindConnection(observer, connection);
            return observer;
        }

        public IEventStream<TEventArgs> Connect<TObserver>(TEventArgs target, TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            Chain(target, observer, out connection);
            return this;
        }

        public IEventStream<TEventArgs> Connect<TObserver>(IEnumerable<TEventArgs> targets, TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            Chain(targets, observer, out connection);
            return this;
        }

        protected override void RemoveConnection(Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            if (updated != null) _maskMap.Remove(updated);
            base.RemoveConnection(updated, completed, errored);
        }

        private uint ToMask(TEventArgs args) => _toMask?.Invoke(args) ?? uint.MaxValue;

        // Disposal
        public override void Dispose()
        {
            try
            {
                base.Dispose();
            }
            finally
            {
                _maskMap.Clear();
            }
        }
    }
}
