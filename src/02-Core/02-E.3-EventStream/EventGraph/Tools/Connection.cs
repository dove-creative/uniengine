using System;
using System.Runtime.ExceptionServices;

namespace BlackThunder.UniEngine.Core
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
            internal EventDispatchScope OrderScope { get; set; }
            internal EventDispatchScope Scope => OrderScope ?? Owner.Node.Scope;
            internal bool IsEmpty => Children.First == null;

            internal Connection(IConnectionOwner owner) => Owner = owner;

            internal void Visit(
                T value,
                long lastVisible,
                ref ExceptionDispatchInfo failure,
                ParallelBatch batch = null)
            {
                for (var child = Children.First; child != null && failure == null && !Scope.Root.Stopped; child = child.Next)
                {
                    if (child.Parent != null && child.Registration <= lastVisible)
                        child.Visit(value, lastVisible, ref failure, batch);
                }
            }

            internal void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure)
            {
                for (var child = Children.First; child != null; child = child.Next)
                {
                    if (signal != Signal.Dispose && failure != null) break;
                    if (child.Parent != null) child.VisitEnd(signal, error, ref failure);
                }
            }
        }
    }
}
