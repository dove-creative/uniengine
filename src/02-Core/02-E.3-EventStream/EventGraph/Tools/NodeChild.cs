using System;
using System.Runtime.ExceptionServices;

namespace BlackThunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        internal enum Signal { Dispose, Completed, Error }

        // What a node can hold as a child. An output gate is one without being a node.
        internal abstract class NodeChild : Link<NodeChild>
        {
            internal Connection Parent;
            internal EventDispatchScope Scope;
            internal abstract bool IsEmpty { get; }

            internal abstract void Visit(
                T value,
                long lastVisible,
                ref ExceptionDispatchInfo failure,
                ParallelBatch batch = null);
            internal abstract void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure);
        }
    }
}
