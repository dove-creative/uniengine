using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace BlackThunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        // Searches by order and visits rows through a chain that remains sorted during mutations.
        private class OrderedNode : Node
        {
            // Property
            internal override bool IsEmpty => _rows.First == null;

            // Internal
            private readonly SortedList<long, Row> _orders = new();
            private Chain<Row> _rows;


            // Content --------------------
            // Construction
            internal OrderedNode() : base(keyed: true) { }

            // Registration
            internal Connection Find(long key) => _orders.TryGetValue(key, out var row) ? row.Connection : null;

            internal Connection FindOrAdd(int order, bool parallel, long registration)
            {
                var key = (long)order * 2 + (parallel ? 0 : 1);
                if (_orders.TryGetValue(key, out var row)) return row.Connection;

                var scope = Find(key ^ 1)?.Scope ?? new EventDispatchScope(Scope.Root);
                row = new Row(this, key, scope, registration);
                _orders.Add(key, row);

                var nextIndex = _orders.IndexOfKey(key) + 1;
                var before = nextIndex < _orders.Count ? _orders.Values[nextIndex] : null;
                _rows.Insert(row, before);
                return row.Connection;
            }

            private void Remove(Row row)
            {
                if (!row.IsConnected) return;

                _orders.Remove(row.Order);
                _rows.Remove(row);
                row.IsConnected = false;
            }

            // Propagation
            internal override void Visit(
                T value,
                long lastVisible,
                ref ExceptionDispatchInfo failure,
                ParallelBatch batch = null)
            {
                batch?.Flush(ref failure);
                if (Parent == null || failure != null || Scope.Root.Stopped) return;

                var row = _rows.First;
                while (row != null && failure == null && !Scope.Root.Stopped)
                {
                    // Parallel and sequential rows of one numeric order commit together.
                    var scope = row.Connection.Scope;
                    scope.Enter();
                    try
                    {
                        do
                        {
                            if (row.IsConnected && row.Registration <= lastVisible)
                            {
                                var pending = row.IsParallel ? new ParallelBatch(value) : null;
                                try { row.Connection.Visit(value, lastVisible, ref failure, pending); }
                                finally { pending?.Flush(ref failure); }
                            }
                            row = row.Next;
                        }
                        while (row != null && row.Connection.Scope == scope
                            && failure == null && !Scope.Root.Stopped);
                    }
                    finally
                    {
                        scope.Exit(ref failure);
                    }
                }
            }

            internal override void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure)
            {
                for (var row = _rows.First; row != null; row = row.Next)
                {
                    if (signal != Signal.Dispose && failure != null) break;
                    if (row.IsConnected) row.Connection.VisitEnd(signal, error, ref failure);
                }
            }

            private class Row : Link<Row>, IConnectionOwner
            {
                // Property
                public Node Node => _owner;

                // Internal
                internal readonly long Order;
                internal bool IsParallel => (Order & 1) == 0;
                internal readonly Connection Connection;
                internal bool IsConnected = true;
                private readonly OrderedNode _owner;


                // Content
                internal Row(OrderedNode owner, long order, EventDispatchScope scope, long registration)
                {
                    _owner = owner;
                    Order = order;
                    Registration = registration;
                    Connection = new Connection(this) { OrderScope = scope };
                }

                public void Remove() => _owner.Remove(this);
            }
        }
    }
}
