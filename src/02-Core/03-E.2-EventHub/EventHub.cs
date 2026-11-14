using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public partial class EventHub<TEventArgs> : IEventHub<TEventArgs>, IDisposable
    {
        // Front
        public object Owner { get; }
        /// <summary>
        /// Counts connections created through Connect or direct event subscriptions.
        /// </summary>
        public int ConnectionCount => _orders.Count;
        public bool IsAwake => !_isSleeping;
        public bool IsDisposed { get; private set; }

        // Control
        public event Action<TEventArgs> Updated
        {
            add
            {
                ThrowIfDisposed();
                if (value == null) return;

                AddUpdated(GetOrAddOrder(value), value, UpdateKind.Typed, null);
            }
            remove => RemoveUpdated(value, UpdateKind.Typed);
        }
        /// <summary>
        /// Subscribing through this interface may box value-type event arguments.
        /// </summary>
        event Action<object> IEventHub.Updated
        {
            add
            {
                ThrowIfDisposed();
                if (value == null) return;

                AddUpdated(GetOrAddOrder(value), value, UpdateKind.Object, null);
            }
            remove => RemoveUpdated(value, UpdateKind.Object);
        }

        public event Action Disposing
        {
            add => AddLifecycleCallback(value, true);
            remove => RemoveLifecycleCallback(value, true);
        }
        public event Action Disposed
        {
            add => AddLifecycleCallback(value, false);
            remove => RemoveLifecycleCallback(value, false);
        }

        // Internal
        internal enum UpdateKind { Typed, Object, Parameterless }

        internal class UpdateSubscription
        {
            internal Delegate Source;
            internal UpdateKind Kind;
            internal EventPath<TEventArgs> Path;
            internal Handle Connection;
        }

        internal class Order
        {
            internal EventHub<TEventArgs> Hub;
            internal object Key;
            internal bool IsConnected = true;
            internal readonly List<UpdateSubscription> UpdateSubscriptions = new();
            internal AdaptiveCollection<Delegate> OnDisposing;
            internal AdaptiveCollection<Delegate> OnDisposed;

            internal Order() { }

            internal static void AddLifecycleCallback(ref AdaptiveCollection<Delegate> callbacks, Delegate callback)
            {
                if (callback == null) return;

                callbacks ??= new AdaptiveCollection<Delegate>();

                if (callbacks.Contains(callback))
                    throw new InvalidOperationException("This lifecycle callback is already registered.");

                InvocationListCache<Delegate>.Get(callback);
                callbacks.Add(callback);
            }

            internal void Clear()
            {
                IsConnected = false;
                Key = null;
                foreach (var subscription in UpdateSubscriptions)
                    subscription.Connection.Dispose();

                UpdateSubscriptions.Clear();
                OnDisposing?.Clear();
                OnDisposed?.Clear();
                OnDisposing = null;
                OnDisposed = null;
            }
        }

        private readonly AdaptiveDictionary<object, Order> _orders = new();
        // All listeners and direct subscriptions share one graph.
        private readonly EventStream<TEventArgs> _updates = new();
        private Func<Action> _onSleep;
        private Action _onAwake;
        private Action _awakeHandler;
        private Token _sleepToken;
        private bool _isSleeping;
        private bool _isUpdating;
        private bool _isDisposing;
        private bool _pendingDispose;


        // Content --------------------
        // Construction
        public EventHub(object owner, Action onDisposing = null, Action onDisposed = null, Func<Action> onSleep = null)
        {
            Owner = owner;

            Disposing += onDisposing;
            Disposed += onDisposed;
            _onSleep = onSleep;
        }

        // Connections
        public IEventHubBuilder<TEventArgs> Connect(object listener)
        {
            ThrowIfDisposed();

            if (listener == null)
                throw new ArgumentNullException(nameof(listener), Ctx("Cannot register a null listener."));

            if (_orders.ContainsKey(listener))
                throw new InvalidOperationException(Ctx($"This Listener already exists: {listener}"));

            return GetBuilder(GetOrAddOrder(listener));
        }
        IEventHubBuilder IEventHub.Connect(object listener) => Connect(listener);
        ILifecycleHubBuilder ILifecycleHub.Connect(object listener) => Connect(listener);
        internal virtual Builder GetBuilder(Order order) => new Builder(order);

        private Order GetOrAddOrder(object key)
        {
            if (_orders.TryGetValue(key, out var order)) return order;
            order = new Order { Hub = this, Key = key };
            _orders.Add(key, order);

            return order;
        }

        public bool HasListener(object listener) => !IsDisposed && _orders.ContainsKey(listener);

        public void Disconnect(object listener)
        {
            if (IsDisposed || listener == null)
                return;

            if (_orders.TryGetValue(listener, out var order))
                RemoveOrder(listener, order);
        }

        // Updates
        /// <summary>
        /// This method can only be invoked by the owner.
        /// </summary>
        public virtual void Update(TEventArgs args)
        {
            ThrowIfDisposed();

            if (_isSleeping || _isUpdating) return;
            _isUpdating = true;

            var completed = false;
            try
            {
                _updates.OnNext(args);
                completed = true;
            }
            finally
            {
                _isUpdating = false;
                PruneUpdateSubscriptions();

                if (_pendingDispose)
                {
                    if (completed)
                        Dispose(true);
                    else
                        ReleaseReferences();
                }
            }
        }

        private void PruneUpdateSubscriptions()
        {
            // The stream releases failed callbacks and paths, even when reporting throws.
            foreach (var item in _orders)
            {
                if (PruneUpdates(item.Value)) RemoveEmptyOrder(item.Value);
            }
        }

        private static bool PruneUpdates(Order order)
        {
            var subscriptions = order.UpdateSubscriptions;
            var retained = 0;
            for (var index = 0; index < subscriptions.Count; index++)
            {
                var subscription = subscriptions[index];
                if (!subscription.Connection.IsDisposed) subscriptions[retained++] = subscription;
            }

            if (retained == subscriptions.Count) return false;
            subscriptions.RemoveRange(retained, subscriptions.Count - retained);
            return true;
        }

        // Registrations
        private void AddUpdated(Order order, Delegate source, UpdateKind kind, EventPath<TEventArgs> path)
        {
            if (source == null) return;

            // Keep the order alive when replacing a failed registration during the same update.
            PruneUpdates(order);
            foreach (var existing in order.UpdateSubscriptions)
            {
                if (existing.Kind == kind && ReferenceEquals(existing.Path, path) && Equals(existing.Source, source))
                    throw new InvalidOperationException(
                        Ctx("This update callback is already registered on this path."));
            }

            var callback = PrepareUpdate(source, kind);
            var connection = path == null ? _updates.Subscribe(callback) : path.Subscribe(callback);

            order.UpdateSubscriptions.Add(new UpdateSubscription
            {
                Source = source,
                Kind = kind,
                Path = path,
                Connection = (Handle)connection,
            });
        }

        private static Action<TEventArgs> PrepareUpdate(Delegate source, UpdateKind kind)
        {
            if (kind == UpdateKind.Typed) return (Action<TEventArgs>)source;

            // One stream subscription checks its handle between all adapted multicast members.
            Action<TEventArgs> adapted = null;
            if (kind == UpdateKind.Object)
            {
                foreach (Action<object> callback in source.GetInvocationList())
                    adapted += args => callback(args);
            }
            else
            {
                foreach (Action callback in source.GetInvocationList())
                    adapted += _ => callback();
            }

            return adapted;
        }

        private void RemoveUpdated(Delegate callback, UpdateKind kind)
        {
            if (IsDisposed || callback == null)
                return;

            if (!_orders.TryGetValue(callback, out var order)) return;
            var subscriptions = order.UpdateSubscriptions;

            for (var index = 0; index < subscriptions.Count; index++)
            {
                var subscription = subscriptions[index];
                if (subscription.Kind != kind
                    || subscription.Path != null
                    || !Equals(subscription.Source, callback))
                    continue;

                subscriptions.RemoveAt(index);
                subscription.Connection.Dispose();
                PruneUpdates(order);
                RemoveEmptyOrder(order);
                break;
            }
        }

        private void AddLifecycleCallback(Action callback, bool disposing)
        {
            ThrowIfDisposed();
            if (callback == null) return;
            var order = GetOrAddOrder(callback);

            if (disposing) Order.AddLifecycleCallback(ref order.OnDisposing, callback);
            else Order.AddLifecycleCallback(ref order.OnDisposed, callback);
        }

        private void RemoveLifecycleCallback(Action callback, bool disposing)
        {
            if (IsDisposed || callback == null) return;
            if (!_orders.TryGetValue(callback, out var order)) return;
            var callbacks = disposing ? order.OnDisposing : order.OnDisposed;

            if (callbacks == null || !callbacks.Remove(callback)) return;
            PruneUpdates(order);
            RemoveEmptyOrder(order);
        }

        private void RemoveEmptyOrder(Order order)
        {
            if (order.UpdateSubscriptions.Count != 0
                || order.OnDisposing?.Count > 0
                || order.OnDisposed?.Count > 0)
                return;

            RemoveOrder(order.Key, order);
        }

        // Sleep
        public bool TrySleep(out IDisposable token, Action onAwake = null)
        {
            ThrowIfDisposed();

            if (_isSleeping)
            {
                token = default;
                return false;
            }

            token = Sleep(new Token(), onAwake);
            return true;
        }
        public IDisposable TrySleep(Action onAwake = null)
        {
            ThrowIfDisposed();
            return _isSleeping ? new Handle(onAwake) : Sleep(new Token(), onAwake);
        }
        public IDisposable Sleep(Action onAwake = null) => Sleep(new Token(), onAwake);
        public IDisposable Sleep(Token token, Action onAwake = null)
        {
            ThrowIfDisposed();

            if (_isSleeping)
                throw new InvalidOperationException(Ctx("Nested Sleep is not allowed."));

            if (token == null || token.IsDisposed)
                throw new ArgumentException(Ctx("Invalid Token could not be connected"), nameof(token));

            _onAwake = onAwake;
            _isSleeping = true;
            _sleepToken = token;
            _awakeHandler ??= Awake;
            token.Action += _awakeHandler;

            try
            {
                _onSleep?.Invoke();
            }
            catch
            {
                token.Action -= _awakeHandler;
                _sleepToken = null;
                _onAwake = null;
                _isSleeping = false;
                throw;
            }

            return token;
        }

        private void Awake()
        {
            if (IsDisposed || _pendingDispose || !_isSleeping)
                return;

            _isSleeping = false;
            _sleepToken = null;

            var onAwake = _onAwake;
            _onAwake = null;
            onAwake?.Invoke();
        }

        // Disposal
        protected void ThrowIfDisposed()
        {
            if (IsDisposed || _pendingDispose || _isDisposing)
                throw new ObjectDisposedException(GetType().Name, Ctx("EventHub has been disposed"));
        }

        private void Dispose(bool disposing)
        {
            if (IsDisposed || _isDisposing) return;
            _pendingDispose = true;

            if (_isUpdating)
            {
                // The stream finishes its root unit before lifecycle callbacks are delivered.
                _updates.Dispose();
                return;
            }

            _isDisposing = true;

            try
            {
                _updates.Dispose();

                if (!disposing)
                {
                    try
                    {
                        Ctx(
                            "An IEventSubject object was garbage collected without being disposed.\n"
                            + "Ensure that Dispose() is explicitly called when the object is no longer needed.")
                            .Write(LogLevel.Warning);
                    }
                    catch (Exception)
                    {
                        // Finalizer diagnostics must never escape the finalizer thread.
                    }

                    return;
                }

                foreach (var item in _orders)
                    InvokeOrder(item.Value, item.Value.OnDisposing);

                IsDisposed = true;

                foreach (var item in _orders)
                    InvokeOrder(item.Value, item.Value.OnDisposed);
            }
            finally
            {
                ReleaseReferences();
            }
        }

        /// <summary>
        /// This method can only be invoked by the owner.
        /// </summary>
        public void Dispose() => Dispose(true);
        ~EventHub() => Dispose(false);

        private void ReleaseReferences()
        {
            IsDisposed = true;
            _pendingDispose = true;
            _isDisposing = false;
            _isSleeping = false;

            if (_sleepToken != null)
                _sleepToken.Action -= _awakeHandler;

            _sleepToken = null;
            _awakeHandler = null;
            _onAwake = null;
            _onSleep = null;

            foreach (var item in _orders)
                item.Value.Clear();

            _orders.Clear();
            GC.SuppressFinalize(this);
        }

        private void InvokeOrder(Order order, AdaptiveCollection<Delegate> callbacks)
        {
            if (callbacks == null) return;

            foreach (var registration in callbacks)
            {
                if (!order.IsConnected) return;

                try
                {
                    foreach (var callback in InvocationListCache<Delegate>.Get(registration))
                    {
                        if (!order.IsConnected || !callbacks.IsCurrentItemActive) break;

                        if (callback is Action action) action();
                        else ((Action<object>)callback)(Owner);
                    }
                }
                catch (Exception exception)
                {
                    RemoveOrder(order.Key, order);
                    ExceptionHandler.Report(exception);
                    return;
                }
            }
        }

        private void RemoveOrder(object listener, Order order)
        {
            if (!order.IsConnected) return;
            order.Clear();

            if (_orders.TryGetValue(listener, out var current) && ReferenceEquals(current, order))
                _orders.Remove(listener);
        }

        private string Ctx(string message) => $"[EventHub: {Owner ?? "null"}] {message}";
    }
}
