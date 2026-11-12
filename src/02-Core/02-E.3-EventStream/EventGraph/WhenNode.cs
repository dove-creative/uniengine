using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        // Routes to one target and keeps every row in creation order for termination.
        private class WhenNode : Node
        {
            // Property
            internal override bool IsEmpty => _rows.First == null;

            // Internal
            // Non-nullable value types skip the null test entirely.
            private static readonly bool _canBeNull = default(T) is null;
            private readonly Dictionary<T, Row> _entries = new(EqualityComparer<T>.Default);
            private Chain<Row> _rows;
            private Row _nullRow;


            // Content --------------------
            // Construction
            internal WhenNode() : base(keyed: true) { }

            // Registration
            internal Connection FindOrAdd(T target, long registration)
            {
                var row = Find(target);
                if (row != null) return row.Connection;

                row = new Row(this, target, registration);

                // Compare before attaching the row, so a failed insertion leaves no empty row.
                if (IsNull(target)) _nullRow = row;
                else _entries.Add(target, row);

                _rows.Insert(row);
                return row.Connection;
            }

            private Row Find(T target) => IsNull(target)
                ? _nullRow
                : _entries.TryGetValue(target, out var row) ? row : null;

            private static bool IsNull(T value) => _canBeNull && value is null;

            private void Remove(Row row)
            {
                if (!row.IsConnected) return;

                if (IsNull(row.Target)) _nullRow = null;
                else if (_entries.Count != 0) _entries.Remove(row.Target);

                _rows.Remove(row);
                row.IsConnected = false;
            }

            // Propagation
            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                Row row;
                try
                {
                    row = Find(value);
                }
                catch (Exception exception)
                {
                    Fail(exception, ref failure);
                    return;
                }

                if (row != null && row.IsConnected && row.Registration <= lastVisible)
                    row.Connection.Visit(value, lastVisible, ref failure);
            }

            internal override void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure)
            {
                if (signal == Signal.Dispose)
                {
                    // Nested routes must also clear their index before releasing a failing target.
                    _entries.Clear();
                    _nullRow = null;
                }

                for (var row = _rows.First; row != null; row = row.Next)
                {
                    if (row.IsConnected) row.Connection.VisitEnd(signal, error, ref failure);
                }
            }

            private class Row : Link<Row>, IConnectionOwner
            {
                // Property
                public Node Node => _owner;

                // Internal
                internal readonly T Target;
                internal readonly Connection Connection;
                internal bool IsConnected = true;
                private readonly WhenNode _owner;


                // Content
                internal Row(WhenNode owner, T target, long registration)
                {
                    _owner = owner;
                    Target = target;
                    Registration = registration;
                    Connection = new Connection(this);
                }

                public void Remove() => _owner.Remove(this);
            }
        }
    }
}
