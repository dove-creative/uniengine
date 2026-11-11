using System;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        internal enum Signal { Dispose, Completed, Error }

        // What a node can hold as a child. An output gate is one without being a node.
        internal abstract class NodeChild : Link<NodeChild>
        {
            internal Connection Parent;
            internal abstract bool IsEmpty { get; }

            internal abstract void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure);
            internal abstract void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure);
        }

        internal abstract class NodeBase : NodeChild
        {
            // A failed step releases its subtree without a terminal notification.
            protected void Fail(Exception exception, ref ExceptionDispatchInfo failure)
            {
                VisitEnd(Signal.Dispose, null, ref failure);
                Report(exception, ref failure);
            }
        }

        // Keyless nodes own one connection. Keyed nodes own rows instead.
        internal class Node : NodeBase, IConnectionOwner
        {
            internal Connection Connection { get; }
            internal override bool IsEmpty => Connection.IsEmpty;
            NodeBase IConnectionOwner.Node => this;

            internal Node() => Connection = new Connection(this);

            void IConnectionOwner.Remove() { }

            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure) =>
                Connection.Visit(value, lastVisible, ref failure);
            internal override void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure) =>
                Connection.VisitEnd(signal, error, ref failure);
        }

        private class DebugNode : Node
        {
            private readonly string _message;
            private readonly LogLevel _logType;

            internal DebugNode(string message, LogLevel logType)
            {
                _message = message;
                _logType = logType;
            }

            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                try
                {
                    $"{value}: {_message}".Write(_logType);
                }
                catch (Exception exception)
                {
                    Fail(exception, ref failure);
                    return;
                }

                base.Visit(value, lastVisible, ref failure);
            }
        }

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
