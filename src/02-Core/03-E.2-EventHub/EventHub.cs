using System;
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
        public int ConnectionCount => _connections.Count;
        public bool IsAwake => !_isSleeping;
        public bool IsDisposed { get; private set; }

        // Control
        public event Action<TEventArgs> Updated
        {
            add
            {
                ThrowIfDisposed();
                if (value == null) return;

                GetOrAddConnection(value).AddUpdate(value, EventHubUpdateKind.Typed, null);
            }
            remove => RemoveUpdated(value, EventHubUpdateKind.Typed);
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

                GetOrAddConnection(value).AddUpdate(value, EventHubUpdateKind.Object, null);
            }
            remove => RemoveUpdated(value, EventHubUpdateKind.Object);
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
        private readonly AdaptiveDictionary<object, Connection> _connections = new();
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
        public EventHub(
            object owner,
            Action onDisposing = null,
            Action onDisposed = null,
            Func<Action> onSleep = null)
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
            if (_connections.ContainsKey(listener))
                throw new InvalidOperationException(Ctx($"This Listener already exists: {listener}"));

            return new EventHubBuilder<TEventArgs>(GetOrAddConnection(listener));
        }

        private Connection GetOrAddConnection(object key)
        {
            if (_connections.TryGetValue(key, out var connection)) return connection;
            connection = new Connection(this, key);
            _connections.Add(key, connection);

            return connection;
        }

        public bool HasListener(object listener) => !IsDisposed && _connections.ContainsKey(listener);

        public void Disconnect(object listener)
        {
            if (IsDisposed || listener == null)
                return;

            if (_connections.TryGetValue(listener, out var connection))
                connection.Dispose();
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

                if (_pendingDispose)
                {
                    if (completed)
                        Dispose(true);
                    else
                        ReleaseReferences();
                }
            }
        }

        // Direct registrations use the delegate as the connection key; the Connection applies the registration rules.
        private void RemoveUpdated(Delegate callback, EventHubUpdateKind kind)
        {
            if (IsDisposed || callback == null)
                return;

            if (_connections.TryGetValue(callback, out var connection))
                connection.RemoveRootUpdate(callback, kind);
        }

        private void AddLifecycleCallback(Action callback, bool disposing)
        {
            ThrowIfDisposed();
            if (callback == null) return;

            var connection = GetOrAddConnection(callback);

            if (disposing) connection.AddDisposing(callback);
            else connection.AddDisposed(callback);
        }

        private void RemoveLifecycleCallback(Action callback, bool disposing)
        {
            if (IsDisposed || callback == null)
                return;

            if (_connections.TryGetValue(callback, out var connection))
                connection.RemoveLifecycleCallback(callback, disposing);
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
                        Ctx(
                            "An IEventSubject object was garbage collected without being disposed.\n" +
                            "Ensure that Dispose() is explicitly called when the object is no longer needed.")
                            .Write(LogLevel.Warning);
                    }
                    catch (Exception)
                    {
                        // Finalizer diagnostics must never escape the finalizer thread.
                    }

                    return;
                }

                foreach (var item in _connections)
                    item.Value.NotifyDisposing();

                IsDisposed = true;

                foreach (var item in _connections)
                    item.Value.NotifyDisposed();
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

            foreach (var item in _connections)
                item.Value.Dispose();

            _connections.Clear();
            GC.SuppressFinalize(this);
        }

        // Called by a connection while it is being disposed.
        private void RemoveConnection(object key, Connection connection)
        {
            // ReleaseReferences clears the whole dictionary after disposing every connection, so removing
            // entries one by one during that traversal would only allocate deferred removals.
            if (IsDisposed && !_isDisposing)
                return;

            if (_connections.TryGetValue(key, out var current) && ReferenceEquals(current, connection))
                _connections.Remove(key);
        }

        private string Ctx(string message) => $"[EventHub: {Owner ?? "null"}] {message}";
    }
}
