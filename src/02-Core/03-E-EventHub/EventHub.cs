using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core.EventStreamCore;

namespace Blackthunder.UniEngine.Core
{
    public partial class EventHub<TEventArgs> : IEventHub<TEventArgs>, IDisposable
    {
        // Front
        public object Owner { get; }
        /// <summary>
        /// Counts only listeners registered through Connect.
        /// </summary>
        public int ConnectionCount => _orders.Count;
        public bool IsAwake => !_isSleeping;
        public bool IsDisposed { get; private set; }

        // Property
        public event Action<TEventArgs> Updated
        {
            add
            {
                ThrowIfDisposed();
                if (value == null) return;

                foreach (var entry in value.GetInvocationList())
                {
                    var callback = (Action<TEventArgs>)entry;
                    AddUpdated(callback, callback, false);
                }
            }
            remove => RemoveUpdated(value);
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

                foreach (var entry in value.GetInvocationList())
                {
                    var callback = (Action<object>)entry;
                    AddUpdated(callback, args => callback(args), true);
                }
            }
            remove => RemoveUpdated(value, true);
        }
        public event Action Disposing
        {
            add { ThrowIfDisposed(); _disposing.Add(value); }
            remove { if (IsDisposed) return; _disposing.Remove(value); }
        }
        public event Action Disposed
        {
            add { ThrowIfDisposed(); _disposed.Add(value); }
            remove { if (IsDisposed) return; _disposed.Remove(value); }
        }

        // Internal
        private enum UpdatePhase
        {
            Listeners,
            Direct,
        }

        private sealed class UpdateSubscription
        {
            internal Delegate Source;
            internal bool IsAdapted;
            internal IDisposable Connection;
        }

        internal class Order
        {
            internal EventHub<TEventArgs> Hub;
            internal bool IsConnected = true;
            internal int SkipCount;
            internal IDisposable UpdateConnection;
            internal Delegate[] UpdateCallbacks;
            public Action<TEventArgs> OnUpdate;
            public Action OnDisposing;
            public Action<object> OnDisposingSelf;
            public Action OnDisposed;
            public Action<object> OnDisposedSelf;

            internal Order() { }

            internal void Clear()
            {
                IsConnected = false;
                UpdateCallbacks = null;
                OnUpdate = null;
                OnDisposing = null;
                OnDisposingSelf = null;
                OnDisposed = null;
                OnDisposedSelf = null;

                var connection = UpdateConnection;
                UpdateConnection = null;
                connection?.Dispose();
            }
        }

        private readonly AdaptiveDictionary<object, Order> _orders = new();
        // One publication defers additions across both update phases.
        private readonly OrderedEventStream<TEventArgs, UpdatePhase> _updates = new()
        {
            TerminationMode = TerminationMode.Complete,
        };
        private readonly List<UpdateSubscription> _updateSubscriptions = new();
        private readonly HubCallbacks<Action> _disposing = new();
        private readonly HubCallbacks<Action> _disposed = new();
        private Func<Action> _onSleep;
        private Action _onAwake;
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
            _disposing.Add(onDisposing);
            _disposed.Add(onDisposed);
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

            var order = new Order { Hub = this };
            _orders.Add(listener, order);
            order.UpdateConnection = _updates.Subscribe(UpdatePhase.Listeners, args => InvokeUpdate(listener, order, args));
            return GetBuilder(order);
        }
        IEventHubBuilder IEventHub.Connect(object listener) => Connect(listener);
        ILifecycleHubBuilder ILifecycleHub.Connect(object listener) => Connect(listener);
        internal virtual Builder GetBuilder(Order order) => new Builder(order);

        public bool HasListener(object listener) => !IsDisposed && _orders.ContainsKey(listener);

        public void Disconnect(object listener)
        {
            if (IsDisposed || listener == null) return;
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

            var orders = new List<Order>(_orders.Count);
            _isUpdating = true;
            var completed = false;
            try
            {
                // Builder changes must also wait until the next publication.
                foreach (var item in _orders)
                {
                    orders.Add(item.Value);
                    item.Value.UpdateCallbacks = item.Value.OnUpdate?.GetInvocationList();
                }

                _updates.OnNext(args);
                completed = true;
            }
            finally
            {
                foreach (var order in orders)
                    order.UpdateCallbacks = null;

                _isUpdating = false;
                if (_pendingDispose)
                {
                    if (completed)
                        Dispose(true);
                    else
                        ReleaseReferences();
                }
            }
        }

        private void InvokeUpdate(object listener, Order order, TEventArgs args)
        {
            if (!order.IsConnected || _pendingDispose) return;
            if (order.SkipCount > 0)
            {
                order.SkipCount--;
                return;
            }

            var callbacks = order.UpdateCallbacks;
            if (callbacks == null) return;

            try
            {
                foreach (var entry in callbacks)
                {
                    var callback = (Action<TEventArgs>)entry;
                    if (!order.IsConnected || _pendingDispose) return;
                    callback(args);
                }
            }
            catch (Exception)
            {
                RemoveOrder(listener, order);
                // The stream owns exception reporting after the listener is disconnected.
                throw;
            }
        }

        // Direct update subscriptions
        private void AddUpdated(Delegate source, Action<TEventArgs> callback, bool isAdapted)
        {
            var subscription = new UpdateSubscription { Source = source, IsAdapted = isAdapted };
            subscription.Connection = _updates.Subscribe(UpdatePhase.Direct, args =>
            {
                try
                {
                    callback(args);
                }
                catch (Exception)
                {
                    RemoveUpdated(subscription);
                    // Let the stream report the callback failure exactly once.
                    throw;
                }
            });
            _updateSubscriptions.Add(subscription);
        }

        private void RemoveUpdated(Delegate callback, bool isAdapted = false)
        {
            if (IsDisposed || callback == null) return;

            var entries = callback.GetInvocationList();
            for (var start = _updateSubscriptions.Count - entries.Length; start >= 0; start--)
            {
                var matches = true;
                for (var offset = 0; offset < entries.Length; offset++)
                {
                    var subscription = _updateSubscriptions[start + offset];
                    if (subscription.IsAdapted != isAdapted || !Equals(subscription.Source, entries[offset]))
                    {
                        matches = false;
                        break;
                    }
                }
                if (!matches) continue;

                for (var offset = 0; offset < entries.Length; offset++)
                    RemoveUpdated(_updateSubscriptions[start]);
                return;
            }
        }

        private void RemoveUpdated(UpdateSubscription subscription)
        {
            _updateSubscriptions.Remove(subscription);
            var connection = subscription.Connection;
            subscription.Connection = null;
            connection?.Dispose();
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
            token.Action += Awake;
            try
            {
                _onSleep?.Invoke();
            }
            catch
            {
                token.Action -= Awake;
                _sleepToken = null;
                _onAwake = null;
                _isSleeping = false;
                throw;
            }

            return token;
        }

        private void Awake()
        {
            if (IsDisposed || _pendingDispose || !_isSleeping) return;
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
                // Stop updates now; lifecycle callbacks wait for the current callback to return.
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
                        Ctx("An IEventSubject object was garbage collected without being disposed.\n"
                            + "Ensure that Dispose() is explicitly called when the object is no longer needed.")
                            .Write(LogLevel.Warning);
                    }
                    catch (Exception)
                    {
                        // Finalizer diagnostics must never escape the finalizer thread.
                    }
                    return;
                }

                var orders = new List<KeyValuePair<object, Order>>(_orders);
                _disposing.Invoke(_disposing.Snapshot(), callback => callback());
                foreach (var item in orders)
                {
                    if (InvokeOrder(item.Key, item.Value, item.Value.OnDisposing?.GetInvocationList(),
                        callback => ((Action)callback)()))
                    {
                        InvokeOrder(item.Key, item.Value, item.Value.OnDisposingSelf?.GetInvocationList(),
                            callback => ((Action<object>)callback)(Owner));
                    }
                }

                IsDisposed = true;
                foreach (var item in orders)
                {
                    if (InvokeOrder(item.Key, item.Value, item.Value.OnDisposed?.GetInvocationList(),
                        callback => ((Action)callback)()))
                    {
                        InvokeOrder(item.Key, item.Value, item.Value.OnDisposedSelf?.GetInvocationList(),
                            callback => ((Action<object>)callback)(Owner));
                    }
                }
                _disposed.Invoke(_disposed.Snapshot(), callback => callback());
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
                _sleepToken.Action -= Awake;
            _sleepToken = null;
            _onAwake = null;
            _onSleep = null;

            foreach (var item in _orders)
                item.Value.Clear();
            _orders.Clear();
            _updateSubscriptions.Clear();
            _disposing.Clear();
            _disposed.Clear();
            GC.SuppressFinalize(this);
        }

        private bool InvokeOrder(object listener, Order order, Delegate[] callbacks,
            Action<Delegate> invoke)
        {
            if (!order.IsConnected) return false;
            if (callbacks == null) return true;

            foreach (var callback in callbacks)
            {
                if (!order.IsConnected) return false;
                try
                {
                    invoke(callback);
                }
                catch (Exception exception)
                {
                    RemoveOrder(listener, order);
                    ExceptionHandler.Report(exception);
                    return false;
                }
            }
            return order.IsConnected;
        }

        private void RemoveOrder(object listener, Order order)
        {
            order.Clear();
            if (_orders.TryGetValue(listener, out var current) && ReferenceEquals(current, order))
                _orders.Remove(listener);
        }

        private string Ctx(string message) => $"[EventHub: {Owner ?? "null"}] {message}";
    }

    internal sealed class HubCallbacks<TCallback> where TCallback : Delegate
    {
        internal sealed class Subscription
        {
            internal TCallback Callback;
            internal Delegate Source;
            internal bool IsAdapted;
            internal bool IsActive = true;
        }

        private readonly List<Subscription> _subscriptions = new();

        internal void Add(TCallback callback)
        {
            if (callback == null) return;
            foreach (var entry in callback.GetInvocationList())
                Add(entry, (TCallback)entry, false);
        }
        internal void Add(Delegate source, TCallback callback, bool isAdapted) =>
            _subscriptions.Add(new Subscription { Source = source, Callback = callback, IsAdapted = isAdapted });

        internal void Remove(Delegate callback, bool isAdapted = false)
        {
            if (callback == null) return;
            var entries = callback.GetInvocationList();
            for (var start = _subscriptions.Count - entries.Length; start >= 0; start--)
            {
                var matches = true;
                for (var offset = 0; offset < entries.Length; offset++)
                {
                    var subscription = _subscriptions[start + offset];
                    if (subscription.IsAdapted != isAdapted || !Equals(subscription.Source, entries[offset]))
                    {
                        matches = false;
                        break;
                    }
                }
                if (!matches) continue;

                for (var offset = 0; offset < entries.Length; offset++)
                    _subscriptions[start + offset].IsActive = false;
                _subscriptions.RemoveRange(start, entries.Length);
                return;
            }
        }

        internal Subscription[] Snapshot() => _subscriptions.ToArray();

        internal void Invoke(Subscription[] snapshot, Action<TCallback> invoke, Func<bool> canContinue = null)
        {
            foreach (var subscription in snapshot)
            {
                if (canContinue != null && !canContinue()) return;
                if (!subscription.IsActive) continue;
                try
                {
                    invoke(subscription.Callback);
                }
                catch (Exception exception)
                {
                    subscription.IsActive = false;
                    _subscriptions.Remove(subscription);
                    ExceptionHandler.Report(exception);
                }
            }
        }

        internal void Clear()
        {
            foreach (var subscription in _subscriptions)
                subscription.IsActive = false;
            _subscriptions.Clear();
        }
    }
}
