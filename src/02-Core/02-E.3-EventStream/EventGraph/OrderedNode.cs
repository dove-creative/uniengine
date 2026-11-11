using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        // Searches by order and visits rows through a chain that remains sorted during mutations.
        private class OrderedNode : NodeBase
        {
            // Property
            internal override bool IsEmpty => _rows.First == null;

            // Internal
            private readonly SortedList<int, Row> _orders = new();
            private Chain<Row> _rows;


            // Content --------------------
            // Registration
            internal Connection FindOrAdd(int order, long registration)
            {
                if (_orders.TryGetValue(order, out var row)) return row.Connection;

                row = new Row(this, order, registration);
                _orders.Add(order, row);

                var nextIndex = _orders.IndexOfKey(order) + 1;
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
            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                for (var row = _rows.First; row != null; row = row.Next)
                {
                    if (row.IsConnected && row.Registration <= lastVisible)
                        row.Connection.Visit(value, lastVisible, ref failure);
                }
            }

            internal override void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure)
            {
                for (var row = _rows.First; row != null; row = row.Next)
                {
                    if (row.IsConnected) row.Connection.VisitEnd(signal, error, ref failure);
                }
            }

            private class Row : Link<Row>, IConnectionOwner
            {
                // Property
                public NodeBase Node => _owner;

                // Internal
                internal readonly int Order;
                internal readonly Connection Connection;
                internal bool IsConnected = true;
                private readonly OrderedNode _owner;


                // Content
                internal Row(OrderedNode owner, int order, long registration)
                {
                    _owner = owner;
                    Order = order;
                    Registration = registration;
                    Connection = new Connection(this);
                }

                public void Remove() => _owner.Remove(this);
            }
        }
    }
}
