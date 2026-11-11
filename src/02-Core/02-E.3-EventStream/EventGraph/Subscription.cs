using System;
using System.Runtime.ExceptionServices;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        private class Subscription : Link<Subscription>
        {
            internal Handle Handle;
            internal bool GraphConnection { get; }

            private readonly Delegate[] _updated;
            private readonly Delegate[] _completed;
            private readonly Delegate[] _errored;
            private bool _notified;

            internal Subscription(Action<T> updated, Action completed, Action<Exception> errored, bool graphConnection)
            {
                GraphConnection = graphConnection;
                _updated = updated?.GetInvocationList();
                _completed = completed?.GetInvocationList();
                _errored = errored?.GetInvocationList();
            }

            internal void Publish(T value)
            {
                if (_updated == null) return;

                foreach (var callback in _updated)
                {
                    if (Handle.IsDisposed) break;

                    ((Action<T>)callback)(value);
                }
            }

            internal void Fail(Exception exception, ref ExceptionDispatchInfo failure)
            {
                if (GraphConnection)
                {
                    // The connected graph already reported its subscriber's failure.
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                    return;
                }

                Handle.Dispose();
                Report(exception, ref failure);
            }

            internal void Notify(Signal signal, Exception error)
            {
                if (_notified) return;
                _notified = true;

                var callbacks = signal == Signal.Completed ? _completed : _errored;
                if (callbacks == null) return;

                foreach (var callback in callbacks)
                {
                    if (Handle.IsDisposed) break;

                    if (signal == Signal.Completed) ((Action)callback)();
                    else ((Action<Exception>)callback)(error);
                }
            }
        }
    }
}
