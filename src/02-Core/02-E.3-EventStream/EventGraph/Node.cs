using System;
using System.Runtime.ExceptionServices;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        // Keyless nodes own one connection. Keyed nodes leave it empty and give one to each row,
        // so they override the members that read it.
        internal class Node : NodeChild, IConnectionOwner
        {
            internal Connection Connection { get; }
            internal override bool IsEmpty => Connection.IsEmpty;
            Node IConnectionOwner.Node => this;

            internal Node() : this(keyed: false) { }
            protected Node(bool keyed)
            {
                if (!keyed) Connection = new Connection(this);
            }

            void IConnectionOwner.Remove() { }

            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure) =>
                Connection.Visit(value, lastVisible, ref failure);
            internal override void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure) =>
                Connection.VisitEnd(signal, error, ref failure);

            // A failed step releases its subtree without a terminal notification.
            protected void Fail(Exception exception, ref ExceptionDispatchInfo failure)
            {
                VisitEnd(Signal.Dispose, null, ref failure);
                Report(exception, ref failure);
            }
        }
    }
}
