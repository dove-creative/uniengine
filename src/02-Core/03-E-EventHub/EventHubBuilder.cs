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
                _order.OnUpdate += onUpdate;
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnUpdated(TEventArgs target, Action<TEventArgs> onUpdate) =>
                OnUpdated(args => EqualityComparer<TEventArgs>.Default.Equals(args, target), onUpdate);

            public IEventHubBuilder<TEventArgs> OnUpdated(TEventArgs target, Action onUpdated) =>
                OnUpdated(args => EqualityComparer<TEventArgs>.Default.Equals(args, target), onUpdated);

            public IEventHubBuilder<TEventArgs> OnUpdated(Func<TEventArgs, bool> filter, Action<TEventArgs> onUpdate)
            {
                _order.Hub.ThrowIfDisposed();
                _order.OnUpdate += args =>
                {
                    if (!filter(args) || onUpdate == null) return;
                    foreach (Action<TEventArgs> callback in onUpdate.GetInvocationList())
                    {
                        if (_order.Hub._pendingDispose || !_order.IsConnected) return;
                        callback(args);
                    }
                };
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnUpdated(Func<TEventArgs, bool> fliter, Action onUpdated)
            {
                _order.Hub.ThrowIfDisposed();
                _order.OnUpdate += args =>
                {
                    if (!fliter(args) || onUpdated == null) return;
                    foreach (Action callback in onUpdated.GetInvocationList())
                    {
                        if (_order.Hub._pendingDispose || !_order.IsConnected) return;
                        callback();
                    }
                };
                return this;
            }

            public IEventHubBuilder<TEventArgs> OnDisposing(Action onDisposing)
            {
                _order.Hub.ThrowIfDisposed();
                _order.OnDisposing += onDisposing;
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnDisposing(Action<object> onDisposing)
            {
                _order.Hub.ThrowIfDisposed();
                _order.OnDisposingSelf += onDisposing;
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnDisposed(Action onDisposed)
            {
                _order.Hub.ThrowIfDisposed();
                _order.OnDisposed += onDisposed;
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnDisposed(Action<object> onDisposed)
            {
                _order.Hub.ThrowIfDisposed();
                _order.OnDisposedSelf += onDisposed;
                return this;
            }
        }
    }
}
