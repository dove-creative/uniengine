using System;
using System.Runtime.ExceptionServices;

namespace Blackthunder.UniEngine.Core
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

            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                for (var sub = Subscriptions.First; sub != null && sub.Registration <= lastVisible; sub = sub.Next)
                {
                    if (sub.Handle.IsDisposed) continue;

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
