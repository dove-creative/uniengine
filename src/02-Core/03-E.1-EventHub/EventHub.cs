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
            internal readonly List<Action<TEventArgs>> UpdateCallbacks = new();
            internal int PublishedCallbackCount;
            internal List<Delegate> OnDisposing;
            internal List<Delegate> OnDisposingSelf;
            internal List<Delegate> OnDisposed;
            internal List<Delegate> OnDisposedSelf;

            internal Order() { }

            internal static void AddLifecycleCallback(ref List<Delegate> callbacks, Delegate callback)
            {
                if (callback == null) return;
                callbacks ??= new List<Delegate>();
                callbacks.AddRange(callback.GetInvocationList());
            }

            internal void Clear()
            {
                IsConnected = false;
                UpdateCallbacks.Clear();
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
        private readonly EventStream<TEventArgs, UpdatePhase> _updates = new();
        private readonly List<UpdateSubscription> _updateSubscriptions = new();
        private readonly HubCallbacks<Action> _disposing = new(static callback => callback());
        private readonly HubCallbacks<Action> _disposed = new(static callback => callback());
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

            _isUpdating = true;
            var completed = false;
            try
            {
                // Freeze the visible prefix for every listener before any callback can add another.
                foreach (var item in _orders)
                    item.Value.PublishedCallbackCount = item.Value.UpdateCallbacks.Count;

                _updates.OnNext(args);
                completed = true;
            }
            finally
            {
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

            try
            {
                for (int i = 0; i < order.PublishedCallbackCount; i++)
                {
                    if (!order.IsConnected || _pendingDispose) return;
                    order.UpdateCallbacks[i](args);
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

                _disposing.Invoke();
                foreach (var item in _orders)
                {
                    InvokeOrder(item.Key, item.Value, item.Value.OnDisposing);
                    InvokeOrder(item.Key, item.Value, item.Value.OnDisposingSelf);
                }

                IsDisposed = true;
                foreach (var item in _orders)
                {
                    InvokeOrder(item.Key, item.Value, item.Value.OnDisposed);
                    InvokeOrder(item.Key, item.Value, item.Value.OnDisposedSelf);
                }
                _disposed.Invoke();
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
            _updateSubscriptions.Clear();
            _disposing.Clear();
            _disposed.Clear();
            GC.SuppressFinalize(this);
        }

        private void InvokeOrder(object listener, Order order, List<Delegate> callbacks)
        {
            if (callbacks == null) return;

            foreach (var callback in callbacks)
            {
                if (!order.IsConnected) return;
                try
                {
                    if (callback is Action action)
                        action();
                    else
                        ((Action<object>)callback)(Owner);
                }
                catch (Exception exception)
                {
                    RemoveOrder(listener, order);
                    ExceptionHandler.Report(exception);
                    return;
                }
            }
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
        private readonly Action<TCallback> _invoke;
        private Subscription[] _snapshot = Array.Empty<Subscription>();

        internal HubCallbacks(Action<TCallback> invoke) => _invoke = invoke;

        internal void Add(TCallback callback)
        {
            if (callback == null) return;
            foreach (var entry in InvocationListCache<Delegate>.Get(callback))
                Add(entry, (TCallback)entry, false);
        }
        internal void Add(Delegate source, TCallback callback, bool isAdapted)
        {
            _subscriptions.Add(new Subscription { Source = source, Callback = callback, IsAdapted = isAdapted });
            if (_snapshot.Length < _subscriptions.Count)
                _snapshot = new Subscription[_subscriptions.Capacity];
        }

        internal void Remove(Delegate callback, bool isAdapted = false)
        {
            if (callback == null) return;
            var entries = InvocationListCache<Delegate>.Get(callback);
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

        internal void Invoke()
        {
            // Owners prevent additions and nested notifications while this snapshot is active.
            _subscriptions.CopyTo(_snapshot);
            var count = _subscriptions.Count;
            for (var index = 0; index < count; index++)
            {
                var subscription = _snapshot[index];
                if (!subscription.IsActive) continue;
                try
                {
                    _invoke(subscription.Callback);
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
            _snapshot = Array.Empty<Subscription>();
        }
    }
}
