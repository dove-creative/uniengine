using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public partial class LifecycleHub : ILifecycleHub
    {
        // Front
        public bool IsDisposed { get; private set; }
        public event Action OnDisposed
        {
            add { ThrowIfDisposed(); _onDisposed.Add(value); }
            remove { if (IsDisposed) return; _onDisposed.Remove(value); }
        }

        // Internal
        private readonly Dictionary<object, Delegate[]> _onDisposedClients = new();
        private readonly HubCallbacks<Action> _onDisposed = new(static callback => callback());
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
            if (IsDisposed || _isDisposing || client == null) return;
            _onDisposedClients.Remove(client);
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
                        ("An ILifecycledSubject object was garbage collected without being disposed.\n"
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
                    if (client.Value == null) continue;
                    foreach (var entry in client.Value)
                    {
                        var callback = (Action)entry;

                        try
                        {
                            callback();
                        }
                        catch (Exception exception)
                        {
                            ExceptionHandler.Report(exception);
                            break;
                        }
                    }
                }
                _onDisposed.Invoke();
            }
            finally
            {
                IsDisposed = true;
                _isDisposing = false;
                _onDisposedClients.Clear();
                _onDisposed.Clear();
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose() => Dispose(true);
        ~LifecycleHub() => Dispose(false);
    }
}
