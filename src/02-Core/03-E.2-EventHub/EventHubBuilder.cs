using System;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public partial class EventHub<TEventArgs>
    {
        /// <summary>
        /// Wraps an update path bound to one listener registration.
        /// </summary>
        public class Builder : IEventHubBuilder<TEventArgs>
        {
            // Internal
            private readonly Order _order;
            // A null path denotes the hub's EventStream root.
            private readonly EventPath<TEventArgs> _path;


            // Content
            internal Builder(Order order, EventPath<TEventArgs> path = null)
            {
                _order = order;
                _path = path;
            }

            public IEventHubBuilder<TEventArgs> Where(Func<TEventArgs, bool> predicate)
            {
                ThrowIfUnavailable();
                var path = _path == null ? _order.Hub._updates.Where(predicate) : _path.Where(predicate);
                return new Builder(_order, path);
            }
            public IEventHubBuilder<TEventArgs> Debug(string message = "Debug", LogLevel logType = LogLevel.Log)
            {
                ThrowIfUnavailable();
                var path = _path == null ? _order.Hub._updates.Debug(message, logType) : _path.Debug(message, logType);
                return new Builder(_order, path);
            }

            public IEventHubBuilder<TEventArgs> OnUpdated(Action<TEventArgs> onUpdate)
            {
                ThrowIfUnavailable();

                _order.Hub.AddUpdated(_order, onUpdate, UpdateKind.Typed, _path);

                return this;
            }
            public IEventHubBuilder<TEventArgs> OnUpdated(Action onUpdated)
            {
                ThrowIfUnavailable();

                _order.Hub.AddUpdated(_order, onUpdated, UpdateKind.Parameterless, _path);

                return this;
            }

            // Preserve the original object delegate instead of forwarding an adapter as its identity.
            IEventHubBuilder IEventHubBuilder.OnUpdated(Action<object> onUpdated)
            {
                ThrowIfUnavailable();
                _order.Hub.AddUpdated(_order, onUpdated, UpdateKind.Object, _path);
                return this;
            }

            public IEventHubBuilder<TEventArgs> OnDisposing(Action onDisposing)
            {
                ThrowIfUnavailable();
                Order.AddLifecycleCallback(ref _order.OnDisposing, onDisposing);
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnDisposing(Action<object> onDisposing)
            {
                ThrowIfUnavailable();
                Order.AddLifecycleCallback(ref _order.OnDisposing, onDisposing);
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnDisposed(Action onDisposed)
            {
                ThrowIfUnavailable();
                Order.AddLifecycleCallback(ref _order.OnDisposed, onDisposed);
                return this;
            }
            public IEventHubBuilder<TEventArgs> OnDisposed(Action<object> onDisposed)
            {
                ThrowIfUnavailable();
                Order.AddLifecycleCallback(ref _order.OnDisposed, onDisposed);
                return this;
            }

            private void ThrowIfUnavailable()
            {
                _order.Hub.ThrowIfDisposed();

                if (!_order.IsConnected)
                    throw new InvalidOperationException(
                        _order.Hub.Ctx("The listener registration has been disconnected."));
            }
        }
    }
}
