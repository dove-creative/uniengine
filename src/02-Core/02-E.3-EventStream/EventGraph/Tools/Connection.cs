using System;
using System.Runtime.ExceptionServices;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        internal interface IConnectionOwner
        {
            Node Node { get; }
            void Remove();
        }

        // Rows and keyless nodes use the same child ordering and publication boundary.
        internal class Connection
        {
            internal Chain<NodeChild> Children;
            internal IConnectionOwner Owner { get; }
            internal bool IsEmpty => Children.First == null;

            internal Connection(IConnectionOwner owner) => Owner = owner;

            internal void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                for (var child = Children.First; child != null; child = child.Next)
                {
                    if (child.Parent != null && child.Registration <= lastVisible)
                        child.Visit(value, lastVisible, ref failure);
                }
            }

            internal void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure)
            {
                for (var child = Children.First; child != null; child = child.Next)
                {
                    if (child.Parent != null) child.VisitEnd(signal, error, ref failure);
                }
            }
        }
    }
}
