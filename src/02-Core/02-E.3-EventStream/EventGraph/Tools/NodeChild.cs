using System;
using System.Runtime.ExceptionServices;

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
    }
}
