using System;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public class Handle : IDisposable
    {
        // Front
        public string Description { get; set; }

        protected event Action Disposed;
        public bool IsDisposed { get; private set; } = false;


        // Content
        protected internal Handle() { }
        public Handle(Action action) => Disposed = action;

        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public virtual void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            try
            {
                var callbacks = Disposed?.GetInvocationList();
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
                Disposed = null;
            }
        }
    }
}
