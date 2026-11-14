using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        internal class Subscription : Link<Subscription>, IEventFailureHandler
        {
            internal Handle Handle;
            internal bool GraphConnection { get; }
            internal bool IsSequential { get; }

            private readonly Delegate[] _updated;
            private readonly Delegate[] _completed;
            private readonly Delegate[] _errored;
            private bool _notified;
            private bool _graphFailure;

            internal Subscription(
                Action<T> updated,
                Action completed,
                Action<Exception> errored,
                bool graphConnection,
                bool sequential = false)
            {
                GraphConnection = graphConnection;
                IsSequential = sequential;
                _updated = updated?.GetInvocationList();
                _completed = completed?.GetInvocationList();
                _errored = errored?.GetInvocationList();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Publish(T value)
            {
                if (_updated == null) return;
                if (IsSequential)
                {
                    PublishStream(value);
                    return;
                }

                foreach (var callback in _updated)
                {
                    ((Action<T>)callback)(value);
                }
            }

            private void PublishStream(T value)
            {
                var callback = (Action<T>)_updated[0];
                var accepted = ((EventStream<T>)callback.Target).CanPublish;
                _graphFailure = false;
                try { callback(value); }
                catch
                {
                    // Entry rejection has not been reported; an admitted graph reports its own failures.
                    _graphFailure = accepted;
                    throw;
                }
            }

            public void Fail(Exception exception, ref ExceptionDispatchInfo failure)
            {
                if (GraphConnection || _graphFailure)
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
