using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        // The exit of a path. It alone keeps subscriptions, so the other nodes only route values.
        private class OutputGate : NodeChild
        {
            internal Chain<Subscription> Subscriptions;
            internal bool IsConnection { get; }
            internal override bool IsEmpty => Subscriptions.First == null;

            internal OutputGate(bool isConnection) => IsConnection = isConnection;

            internal override void Visit(
                T value,
                long lastVisible,
                ref ExceptionDispatchInfo failure,
                ParallelBatch batch = null)
            {
                if (batch != null)
                {
                    Collect(value, lastVisible, ref failure, batch);
                    return;
                }

                var scope = EventDispatchScope.Current;
                var receiver = scope.Receiver;
                try
                {
                    for (var sub = Subscriptions.First;
                        sub != null && sub.Registration <= lastVisible && failure == null;
                        sub = sub.Next)
                    {
                        if (sub.Handle.IsDisposed) continue;
                        scope.Receiver = sub;
                        try { sub.Publish(value); }
                        catch (Exception exception) { sub.Fail(exception, ref failure); }
                    }
                }
                finally
                {
                    scope.Receiver = receiver;
                }
            }

            private void Collect(T value, long lastVisible, ref ExceptionDispatchInfo failure, ParallelBatch batch)
            {
                for (var sub = Subscriptions.First;
                    sub != null && sub.Registration <= lastVisible && failure == null && !Scope.Root.Stopped;
                    sub = sub.Next)
                {
                    if (sub.Handle.IsDisposed) continue;

                    if (!IsConnection && !sub.IsSequential)
                    {
                        batch.Add(sub);
                        continue;
                    }

                    batch.Flush(ref failure);
                    if (failure != null || Scope.Root.Stopped) break;
                    if (Parent == null || sub.Handle.IsDisposed) continue;

                    try
                    {
                        sub.Publish(value);
                    }
                    catch (Exception exception)
                    {
                        sub.Fail(exception, ref failure);
                    }
                }
            }

            internal override void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure)
            {
                for (var sub = Subscriptions.First; sub != null; sub = sub.Next)
                {
                    if (signal != Signal.Dispose && failure != null) break;
                    if (sub.Handle.IsDisposed) continue;

                    if (signal == Signal.Dispose)
                    {
                        sub.Handle.Dispose();
                        continue;
                    }

                    try
                    {
                        sub.Notify(signal, error);
                    }
                    catch (Exception exception)
                    {
                        sub.Fail(exception, ref failure);
                    }
                }
            }
        }
    }
}
