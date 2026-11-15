using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace BlackThunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        private class WhereNode : Node
        {
            // Internal
            private Chain<Row> _rows;
            private Dictionary<Func<T, bool>, Row> _index;
            private int _count;

            internal override bool IsEmpty => _rows.First == null;


            // Content --------------------
            // Construction
            internal WhereNode() : base(keyed: true) { }

            // Registration
            internal Connection FindOrAdd(Func<T, bool> predicate, long registration)
            {
                if (_index != null)
                {
                    if (_index.TryGetValue(predicate, out var existing)) return existing.Connection;
                }
                else
                {
                    for (var row = _rows.First; row != null; row = row.Next)
                    {
                        if (row.Predicate == predicate) return row.Connection;
                    }

                    if (_count == RowIndexThreshold)
                    {
                        _index = new Dictionary<Func<T, bool>, Row>(_count + 1);
                        for (var row = _rows.First; row != null; row = row.Next)
                            _index.Add(row.Predicate, row);
                    }
                }

                var added = new Row(this, predicate) { Registration = registration };
                _index?.Add(predicate, added);
                _rows.Insert(added);
                _count++;
                return added.Connection;
            }

            internal Connection Find(Func<T, bool> predicate)
            {
                if (_index != null)
                    return _index.TryGetValue(predicate, out var existing) ? existing.Connection : null;

                for (var row = _rows.First; row != null; row = row.Next)
                {
                    if (row.Predicate == predicate) return row.Connection;
                }
                return null;
            }

            private void Remove(Row row)
            {
                if (!row.IsConnected) return;

                _index?.Remove(row.Predicate);
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
                for (var row = _rows.First; row != null && failure == null && !Scope.Root.Stopped; row = row.Next)
                {
                    if (!row.IsConnected || row.Registration > lastVisible) continue;

                    bool matches;
                    try
                    {
                        matches = row.Predicate(value);
                    }
                    catch (Exception exception)
                    {
                        row.Connection.VisitEnd(Signal.Dispose, null, ref failure);
                        Report(exception, ref failure);
                        continue;
                    }

                    if (matches && row.IsConnected)
                        row.Connection.Visit(value, lastVisible, ref failure, batch);
                }
            }

            internal override void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure)
            {
                for (var row = _rows.First; row != null; row = row.Next)
                {
                    if (signal != Signal.Dispose && failure != null) break;
                    if (row.IsConnected)
                        row.Connection.VisitEnd(signal, error, ref failure);
                }
            }

            private class Row : Link<Row>, IConnectionOwner
            {
                internal Func<T, bool> Predicate { get; }
                internal Connection Connection { get; }
                internal bool IsConnected = true;
                private readonly WhereNode _node;

                Node IConnectionOwner.Node => _node;

                internal Row(WhereNode node, Func<T, bool> predicate)
                {
                    _node = node;
                    Predicate = predicate;
                    Connection = new Connection(this);
                }

                void IConnectionOwner.Remove() => _node.Remove(this);
            }
        }
    }
}
