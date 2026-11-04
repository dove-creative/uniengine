using System;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public class Handle : IDisposable
    {
        // Front
        public string Description { get; set; }

        protected event Action Disposed
        {
            add => SetCallbacks(_disposed + value);
            remove => SetCallbacks(_disposed - value);
        }
        public bool IsDisposed { get; private set; } = false;

        // Internal
        private Action _disposed;
        private Delegate[] _callbacks;


        // Content
        protected internal Handle() { }
        public Handle(Action action) => SetCallbacks(action);

        private void SetCallbacks(Action action)
        {
            if (ReferenceEquals(_disposed, action)) return;
            _callbacks = action?.GetInvocationList();
            _disposed = action;
        }

        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public virtual void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            var callbacks = _callbacks;

            try
            {
                if (callbacks == null) return;

                foreach (var entry in callbacks)
                {
                    var callback = (Action)entry;

                    try
                    {
                        callback();
                    }
                    catch (Exception exception)
                    {
                        Disposed -= callback;
                        ExceptionHandler.Report(exception);
                    }
                }
            }
            finally
            {
                SetCallbacks(null);
            }
        }
    }
}
