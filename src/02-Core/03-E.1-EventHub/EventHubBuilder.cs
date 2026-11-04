using System;
using System.Collections.Generic;

namespace Blackthunder.UniEngine.Core
{
    public partial class EventHub<TEventArgs>
    {
        /// <summary>
        /// Configures one listener registration and must not be reused for another listener.
        /// </summary>
        public class Builder : IEventHubBuilder<TEventArgs>
        {
            // Internal
            private readonly Order _order;


            // Content
            internal Builder(Order order) => _order = order;

            public IEventHubBuilder<TEventArgs> OnUpdated(Action<TEventArgs> onUpdate)
            {
                _order.Hub.ThrowIfDisposed();
                if (onUpdate != null)
                {
                    foreach (var entry in InvocationListCache<Action<TEventArgs>>.Get(onUpdate))
                        _order.UpdateCallbacks.Add((Action<TEventArgs>)entry);
                }
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnUpdated(TEventArgs target, Action<TEventArgs> onUpdate)
            {
                var comparer = EqualityComparer<TEventArgs>.Default;
                return OnUpdated(args => comparer.Equals(args, target), onUpdate);
            }

            public IEventHubBuilder<TEventArgs> OnUpdated(TEventArgs target, Action onUpdated)
            {
                var comparer = EqualityComparer<TEventArgs>.Default;
                return OnUpdated(args => comparer.Equals(args, target), onUpdated);
            }

            public IEventHubBuilder<TEventArgs> OnUpdated(Func<TEventArgs, bool> filter, Action<TEventArgs> onUpdate)
            {
                _order.Hub.ThrowIfDisposed();
                var callbacks = onUpdate == null ? null : InvocationListCache<Action<TEventArgs>>.Get(onUpdate);
                _order.UpdateCallbacks.Add(args =>
                {
                    if (!filter(args) || callbacks == null) return;
                    foreach (var entry in callbacks)
                    {
                        var callback = (Action<TEventArgs>)entry;
                        if (_order.Hub._pendingDispose || !_order.IsConnected) return;
                        callback(args);
                    }
                });
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnUpdated(Func<TEventArgs, bool> fliter, Action onUpdated)
            {
                _order.Hub.ThrowIfDisposed();
                var callbacks = onUpdated == null ? null : InvocationListCache<Action>.Get(onUpdated);
                _order.UpdateCallbacks.Add(args =>
                {
                    if (!fliter(args) || callbacks == null) return;
                    foreach (var entry in callbacks)
                    {
                        var callback = (Action)entry;
                        if (_order.Hub._pendingDispose || !_order.IsConnected) return;
                        callback();
                    }
                });
                return this;
            }

            public IEventHubBuilder<TEventArgs> OnDisposing(Action onDisposing)
            {
                _order.Hub.ThrowIfDisposed();
                Order.AddLifecycleCallback(ref _order.OnDisposing, onDisposing);
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnDisposing(Action<object> onDisposing)
            {
                _order.Hub.ThrowIfDisposed();
                Order.AddLifecycleCallback(ref _order.OnDisposingSelf, onDisposing);
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnDisposed(Action onDisposed)
            {
                _order.Hub.ThrowIfDisposed();
                Order.AddLifecycleCallback(ref _order.OnDisposed, onDisposed);
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnDisposed(Action<object> onDisposed)
            {
                _order.Hub.ThrowIfDisposed();
                Order.AddLifecycleCallback(ref _order.OnDisposedSelf, onDisposed);
                return this;
            }
        }
    }
}
