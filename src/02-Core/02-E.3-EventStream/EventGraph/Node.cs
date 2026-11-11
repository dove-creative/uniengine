using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        internal enum Signal { Dispose, Completed, Error }

        // What a node can hold as a child. An output gate is one without being a node.
        internal abstract class NodeChild : Link<NodeChild>
        {
            internal NodeBase Parent;
            internal abstract bool IsEmpty { get; }

            internal abstract void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure);
            internal abstract void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure);
        }

        // What holds children. It visits them in the order of its list.
        internal abstract class NodeBase : NodeChild
        {
            internal Chain<NodeChild> Children;
            internal override bool IsEmpty => Children.First == null;

            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                for (var child = Children.First; child != null; child = child.Next)
                {
                    if (child.Parent != null && child.Registration <= lastVisible)
                        child.Visit(value, lastVisible, ref failure);
                }
            }

            internal override void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure)
            {
                for (var child = Children.First; child != null; child = child.Next)
                {
                    if (child.Parent != null) child.VisitEnd(signal, error, ref failure);
                }
            }

            // A failed step releases its subtree without a terminal notification.
            protected void Fail(Exception exception, ref ExceptionDispatchInfo failure)
            {
                VisitEnd(Signal.Dispose, null, ref failure);
                Report(exception, ref failure);
            }
        }

        // Keeps children in the order they are added.
        internal class Node : NodeBase { }

        // Keeps one child per order. The list stays sorted by order and is what a publication visits,
        // because the index cannot change while it is enumerated.
        private class OrderedNode : NodeBase
        {
            private readonly SortedList<int, Node> _orders = new();

            internal Node Find(int order) => _orders.TryGetValue(order, out var node) ? node : null;

            // The child of the next larger order, so that a new order goes in front of it.
            internal Node FindNext(int order)
            {
                for (int i = 0; i < _orders.Count; i++)
                {
                    if (_orders.Keys[i] > order) return _orders.Values[i];
                }

                return null;
            }

            internal void Add(int order, Node node) => _orders.Add(order, node);

            internal void Remove(Node node)
            {
                var index = _orders.IndexOfValue(node);
                if (index >= 0) _orders.RemoveAt(index);
            }
        }

        // Keeps one entry per mask. Matching entries retain their creation order.
        private class FlagsNode : NodeBase
        {
            internal FlagsEntry Find(ulong mask)
            {
                for (var child = Children.First; child != null; child = child.Next)
                {
                    var entry = (FlagsEntry)child;
                    if (entry.Mask == mask) return entry;
                }

                return null;
            }

            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                var bits = _readFlags(value);
                if (bits == 0)
                    return;

                for (var child = Children.First; child != null; child = child.Next)
                {
                    if (child.Parent != null && child.Registration <= lastVisible
                        && bits.HasAny(((FlagsEntry)child).Mask))
                        child.Visit(value, lastVisible, ref failure);
                }
            }
        }

        private class FlagsEntry : Node
        {
            internal ulong Mask { get; }

            internal FlagsEntry(ulong mask) => Mask = mask;
        }

        // Routes to at most one target while retaining all entries in the child chain for termination.
        private class WhenNode : NodeBase
        {
            // Non-nullable value types skip the null test entirely; the runtime specializes nullable tests.
            private static readonly bool _canBeNull = default(T) is null;
            private readonly Dictionary<T, WhenEntry> _entries = new(EqualityComparer<T>.Default);
            private WhenEntry _nullEntry;

            internal WhenEntry Find(T target) => IsNull(target)
                ? _nullEntry
                : _entries.TryGetValue(target, out var entry) ? entry : null;

            internal void Add(WhenEntry entry)
            {
                if (IsNull(entry.Target)) _nullEntry = entry;
                else _entries.Add(entry.Target, entry);
            }

            internal void Remove(WhenEntry entry)
            {
                if (IsNull(entry.Target)) _nullEntry = null;
                else if (_entries.Count != 0) _entries.Remove(entry.Target);
            }

            private static bool IsNull(T value) => _canBeNull && value is null;

            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                WhenEntry entry;
                try
                {
                    entry = Find(value);
                }
                catch (Exception exception)
                {
                    Fail(exception, ref failure);
                    return;
                }

                if (entry != null && entry.Parent != null && entry.Registration <= lastVisible)
                    entry.Visit(value, lastVisible, ref failure);
            }

            internal override void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure)
            {
                if (signal == Signal.Dispose)
                {
                    // Every nested value route must release without calling a failing comparer again.
                    _entries.Clear();
                    _nullEntry = null;
                }

                base.VisitEnd(signal, error, ref failure);
            }
        }

        private class WhenEntry : Node
        {
            internal T Target { get; }

            internal WhenEntry(T target) => Target = target;
        }

        internal class WhereNode : Node
        {
            internal Func<T, bool> Predicate { get; }

            internal WhereNode(Func<T, bool> predicate) => Predicate = predicate;

            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                bool matches;
                try
                {
                    matches = Predicate(value);
                }
                catch (Exception exception)
                {
                    Fail(exception, ref failure);
                    return;
                }

                if (matches) base.Visit(value, lastVisible, ref failure);
            }
        }

        private class DebugNode : Node
        {
            private readonly string _message;
            private readonly LogLevel _logType;

            internal DebugNode(string message, LogLevel logType)
            {
                _message = message;
                _logType = logType;
            }

            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                try
                {
                    $"{value}: {_message}".Write(_logType);
                }
                catch (Exception exception)
                {
                    Fail(exception, ref failure);
                    return;
                }

                base.Visit(value, lastVisible, ref failure);
            }
        }

        // The exit of a path. It alone keeps subscriptions, so the other nodes only route values.
        private class OutputGate : NodeChild
        {
            internal Chain<Subscription> Subscriptions;
            internal bool IsConnection { get; }
            internal override bool IsEmpty => Subscriptions.First == null;

            internal OutputGate(bool isConnection) => IsConnection = isConnection;

            internal override void Visit(T value, long lastVisible, ref ExceptionDispatchInfo failure)
            {
                for (var sub = Subscriptions.First; sub != null && sub.Registration <= lastVisible; sub = sub.Next)
                {
                    if (sub.Handle.IsDisposed) continue;

                    try
                    {
                        sub.Publish(value);
                    }
                    catch (Exception exception)
                    {
                        sub.Fail(exception, ref failure);
                    }
                }
            }

            internal override void VisitEnd(Signal signal, Exception error, ref ExceptionDispatchInfo failure)
            {
                for (var sub = Subscriptions.First; sub != null; sub = sub.Next)
                {
                    if (sub.Handle.IsDisposed) continue;

                    if (signal == Signal.Dispose)
                    {
                        sub.Handle.Dispose();
                        continue;
                    }

                    try
                    {
                        sub.Notify(signal, error);
                    }
                    catch (Exception exception)
                    {
                        sub.Fail(exception, ref failure);
                    }
                }
            }
        }
    }
}
