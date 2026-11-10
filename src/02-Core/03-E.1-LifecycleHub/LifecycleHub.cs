using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public partial class LifecycleHub : ILifecycleHub
    {
        // Front
        public bool IsDisposed { get; private set; }

        // Control
        public event Action OnDisposed
        {
            add
            {
                ThrowIfDisposed();
                if (value == null) return;

                Register(value, value);
            }
            remove => Disconnect(value);
        }

        // Internal
        private readonly Dictionary<object, Action> _onDisposedClients = new();
        private bool _isDisposing;


        // Content --------------------
        // Construction
        public LifecycleHub() { }

        // Connections
        public ILifecycleHubBuilder Connect(object client)
        {
            ThrowIfDisposed();
            return new Builder(this, client);
        }

        public void Disconnect(object client)
        {
            if (IsDisposed || _isDisposing || client == null)
                return;

            _onDisposedClients.Remove(client);
        }

        // Direct subscriptions and clients share one key space, so a key registers only once.
        private void Register(object key, Action onDisposed)
        {
            if (_onDisposedClients.ContainsKey(key))
                throw new InvalidOperationException("This client or callback is already registered.");

            _onDisposedClients.Add(key, onDisposed);
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed || _isDisposing)
                throw new ObjectDisposedException(GetType().Name);
        }

        // Disposal
        private void Dispose(bool disposing)
        {
            if (IsDisposed || _isDisposing) return;
            _isDisposing = true;

            try
            {
                if (!disposing)
                {
                    try
                    {
                        ("An ILifecycleSubject object was garbage collected without being disposed.\n"
                            + "Ensure that Dispose() is explicitly called when the object is no longer needed.")
                            .Write(LogLevel.Warning);
                    }
                    catch (Exception)
                    {
                        // Finalizer diagnostics must never escape the finalizer thread.
                    }

                    return;
                }

                IsDisposed = true;

                foreach (var client in _onDisposedClients)
                {
                    try
                    {
                        client.Value?.Invoke();
                    }
                    catch (Exception exception)
                    {
                        ExceptionHandler.Report(exception);
                    }
                }
            }
            finally
            {
                IsDisposed = true;
                _isDisposing = false;
                _onDisposedClients.Clear();
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose() => Dispose(true);
        ~LifecycleHub() => Dispose(false);
    }
}
