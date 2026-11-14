using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        private class FlagsNode : Node
        {
            // Internal
            private Chain<Row> _rows;
            private Dictionary<ulong, Row> _index;
            private int _count;

            internal override bool IsEmpty => _rows.First == null;


            // Content --------------------
            // Construction
            internal FlagsNode() : base(keyed: true) { }

            // Registration
            internal Connection FindOrAdd(ulong mask, long registration)
            {
                if (_index != null)
                {
                    if (_index.TryGetValue(mask, out var existing)) return existing.Connection;
                }
                else
                {
                    for (var row = _rows.First; row != null; row = row.Next)
                    {
                        if (row.Mask == mask) return row.Connection;
                    }

                    if (_count == RowIndexThreshold)
                    {
                        _index = new Dictionary<ulong, Row>(_count + 1);
                        for (var row = _rows.First; row != null; row = row.Next)
                            _index.Add(row.Mask, row);
                    }
                }

                var added = new Row(this, mask) { Registration = registration };
                _index?.Add(mask, added);
                _rows.Insert(added);
                _count++;
                return added.Connection;
            }

            internal Connection Find(ulong mask)
            {
                if (_index != null)
                    return _index.TryGetValue(mask, out var existing) ? existing.Connection : null;

                for (var row = _rows.First; row != null; row = row.Next)
                {
                    if (row.Mask == mask) return row.Connection;
                }
                return null;
            }

            private void Remove(Row row)
            {
                if (!row.IsConnected) return;

                _index?.Remove(row.Mask);
                _rows.Remove(row);
                row.IsConnected = false;
                if (--_count == 0) _index = null;
            }

            // Propagation
            internal override void Visit(
                T value,
                long lastVisible,
                ref ExceptionDispatchInfo failure,
                ParallelBatch batch = null)
            {
                var bits = _readFlags(value);
                if (bits == 0) return;

                for (var row = _rows.First; row != null && failure == null && !Scope.Root.Stopped; row = row.Next)
                {
                    if (row.IsConnected && row.Registration <= lastVisible && (bits & row.Mask) != 0)
                        row.Connection.Visit(value, lastVisible, ref failure, batch);
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
                internal ulong Mask { get; }
                internal Connection Connection { get; }
                internal bool IsConnected = true;
                private readonly FlagsNode _node;

                Node IConnectionOwner.Node => _node;

                internal Row(FlagsNode node, ulong mask)
                {
                    _node = node;
                    Mask = mask;
                    Connection = new Connection(this);
                }

                void IConnectionOwner.Remove() => _node.Remove(this);
            }
        }
    }
}
