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

        private class Subscription : Link<Subscription>
        {
            internal Handle Handle;
            internal bool GraphConnection { get; }

            private readonly Delegate[] _updated;
            private readonly Delegate[] _completed;
            private readonly Delegate[] _errored;
            private bool _notified;

            internal Subscription(Action<T> updated, Action completed, Action<Exception> errored, bool graphConnection)
            {
                GraphConnection = graphConnection;
                _updated = updated?.GetInvocationList();
                _completed = completed?.GetInvocationList();
                _errored = errored?.GetInvocationList();
            }

            internal void Publish(T value)
            {
                if (_updated == null) return;

                foreach (var callback in _updated)
                {
                    if (Handle.IsDisposed) break;

                    ((Action<T>)callback)(value);
                }
            }

            internal void Fail(Exception exception, ref ExceptionDispatchInfo failure)
            {
                if (GraphConnection)
                {
                    // The connected graph already reported its subscriber's failure.
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                    return;
                }

                Handle.Dispose();
                Report(exception, ref failure);
            }

            internal void Notify(Signal signal, Exception error)
            {
                if (_notified) return;
                _notified = true;

                var callbacks = signal == Signal.Completed ? _completed : _errored;
                if (callbacks == null) return;

                foreach (var callback in callbacks)
                {
                    if (Handle.IsDisposed) break;

                    if (signal == Signal.Completed) ((Action)callback)();
                    else ((Action<Exception>)callback)(error);
                }
            }
        }

        internal abstract class Link<TLink> where TLink : Link<TLink>
        {
            internal long Registration;
            internal TLink Previous;
            internal TLink Next;
        }

        internal struct Chain<TLink> where TLink : Link<TLink>
        {
            internal TLink First { get; private set; }
            private TLink _last;

            internal void Insert(TLink link, TLink before = null)
            {
                link.Next = before;
                link.Previous = before == null ? _last : before.Previous;

                if (link.Previous == null) First = link;
                else link.Previous.Next = link;

                if (before == null) _last = link;
                else before.Previous = link;
            }

            internal void Remove(TLink link)
            {
                if (link.Previous == null) First = link.Next;
                else link.Previous.Next = link.Next;

                if (link.Next == null) _last = link.Previous;
                else link.Next.Previous = link.Previous;

                link.Previous = null;
                // A cursor on a removed item can still reach the following live sibling.
            }
        }
    }
}
