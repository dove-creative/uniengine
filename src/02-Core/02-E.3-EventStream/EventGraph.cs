using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    internal sealed class EventGraph<T> : IDisposable
    {
        // Front
        internal int NodeCount { get; private set; }
        internal bool IsPublishing { get; private set; }

        // Internal
        private readonly Node _root = new();
        private readonly Action _onEmpty;
        private const int IndexThreshold = 8;
        private long _lastRegistration;
        private bool _closed;


        // Content --------------------
        // Registration
        internal EventGraph(Action onEmpty) => _onEmpty = onEmpty;

        internal Handle Subscribe(EventPath<T> path, Action<T> updated, Action completed, Action<Exception> errored,
            bool graphConnection = false)
        {
            var node = RegisterPath(path);
            return Attach(node, updated, completed, errored, graphConnection, null);
        }

        internal Handle ConnectBranch(EventPath<T> path, EventStream<T> branch)
        {
            var parent = RegisterPath(path);
            var node = AddChild(parent, new Node());
            return Attach(node, branch.OnNext, branch.CompleteFromParent, branch.ErrorFromParent, true, branch.Dispose);
        }

        private Node RegisterPath(EventPath<T> path)
        {
            if (_closed) throw new ObjectDisposedException(nameof(EventStream<T>));
            _lastRegistration++;
            return Resolve(path);
        }

        private Node Resolve(EventPath<T> path)
        {
            if (path == null) return _root;
            var parent = Resolve(path.Parent);
            if (path.Operation == PathOperation.Group)
            {
                for (var group = parent.Children.First; group != null; group = group.Next)
                {
                    if (group.Path?.Operation != PathOperation.Group) continue;
                    if (group.Path.Count == path.Count) return group;
                    if (group.Path.Count > path.Count)
                        return AddChild(parent, new Node { Path = path }, group);
                }
            }
            if (path.Operation == PathOperation.Where)
            {
                var existing = FindWhere(parent, path.Filter);
                if (existing != null) return existing;
            }
            return AddChild(parent, new Node { Path = path, Remaining = path.Count });
        }

        private static Node FindWhere(Node parent, Filter<T> filter)
        {
            if (parent.WhereIndex == null && parent.ChildCount >= IndexThreshold)
            {
                parent.WhereIndex = new Dictionary<Filter<T>, Node>();
                for (var node = parent.Children.First; node != null; node = node.Next)
                    if (node.Path?.Operation == PathOperation.Where)
                        parent.WhereIndex.Add(node.Path.Filter, node);
            }
            if (parent.WhereIndex != null)
                return parent.WhereIndex.TryGetValue(filter, out var match) ? match : null;

            for (var child = parent.Children.First; child != null; child = child.Next)
            {
                if (child.Path?.Operation == PathOperation.Where && ReferenceEquals(child.Path.Filter, filter))
                    return child;
            }
            return null;
        }

        private Node AddChild(Node parent, Node child, Node before = null)
        {
            child.Registration = _lastRegistration;
            child.Parent = parent;
            parent.Children.Insert(child, before);
            parent.ChildCount++;
            if (child.Path?.Operation == PathOperation.Where) parent.WhereIndex?.Add(child.Path.Filter, child);
            NodeCount++;
            return child;
        }

        private Handle Attach(Node node, Action<T> updated, Action completed, Action<Exception> errored,
            bool graphConnection, Action disconnected)
        {
            var subscription = new Subscription(updated, completed, errored, graphConnection) { Registration = _lastRegistration };
            subscription.Handle = new Handle(() =>
            {
                node.Subscriptions.Remove(subscription);
                Prune(node);
                disconnected?.Invoke();
                if (!_closed && _root.Children.First == null && _root.Subscriptions.First == null)
                    _onEmpty?.Invoke();
            });
            node.Subscriptions.Insert(subscription);
            return subscription.Handle;
        }

        private void Prune(Node node)
        {
            while (node.Parent != null && node.Children.First == null && node.Subscriptions.First == null)
            {
                var parent = node.Parent;
                parent.Children.Remove(node);
                if (node.Path?.Operation == PathOperation.Where)
                    parent.WhereIndex?.Remove(node.Path.Filter);
                if (--parent.ChildCount < IndexThreshold / 2) parent.WhereIndex = null;
                node.Parent = null;
                NodeCount--;
                node = parent;
            }
        }

        // Propagation
        internal void OnNext(T value)
        {
            if (_closed) return;
            if (IsPublishing) throw new InvalidOperationException("Cannot publish recursively to the same stream.");
            IsPublishing = true;
            try
            {
                ExceptionDispatchInfo failure = null;
                Visit(_root, value, _lastRegistration, ref failure);
                failure?.Throw();
            }
            finally
            {
                IsPublishing = false;
            }
        }

        private static void Visit(Node node, T value, long lastVisible, ref ExceptionDispatchInfo failure)
        {
            var path = node.Path;
            if (path != null)
            {
                try
                {
                    switch (path.Operation)
                    {
                        case PathOperation.Where:
                            if (!path.Filter.Predicate(value)) return;
                            break;
                        case PathOperation.Skip:
                            if (node.Remaining > 0)
                            {
                                node.Remaining--;
                                return;
                            }
                            break;
                        case PathOperation.SkipWhile:
                            if (!node.SkipFinished)
                            {
                                if (path.Filter.Predicate(value)) return;
                                node.SkipFinished = true;
                            }
                            break;
                        case PathOperation.Take:
                            if (node.Remaining > 0) break;
                            End(node, Signal.Completed, null, ref failure);
                            return;
                        case PathOperation.TakeWhile:
                            if (path.Filter.Predicate(value)) break;
                            End(node, Signal.Completed, null, ref failure);
                            return;
                        case PathOperation.Debug:
                            $"{value}: {path.Message}".Write(path.LogType);
                            break;
                    }
                }
                catch (Exception exception)
                {
                    End(node, Signal.Dispose, null, ref failure);
                    Report(exception, ref failure);
                    return;
                }
            }

            for (var sub = node.Subscriptions.First; sub != null && sub.Registration <= lastVisible; sub = sub.Next)
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

            for (var child = node.Children.First; child != null; child = child.Next)
            {
                if (child.Parent != null && child.Registration <= lastVisible)
                    Visit(child, value, lastVisible, ref failure);
            }

            if (path?.Operation == PathOperation.Take && --node.Remaining == 0)
                End(node, Signal.Completed, null, ref failure);
        }

        private static void Report(Exception exception, ref ExceptionDispatchInfo failure)
        {
            try
            {
                ExceptionHandler.Report(exception);
            }
            catch (Exception reportFailure)
            {
                failure ??= ExceptionDispatchInfo.Capture(reportFailure);
            }
        }

        // Termination
        internal void Terminate(Exception error, bool completed)
        {
            if (_closed) return;
            _closed = true;
            ExceptionDispatchInfo failure = null;
            End(_root, completed ? Signal.Completed : Signal.Error, error, ref failure);
            failure?.Throw();
        }

        public void Dispose()
        {
            _closed = true;
            ExceptionDispatchInfo failure = null;
            End(_root, Signal.Dispose, null, ref failure);
            failure?.Throw();
        }

        private static void End(Node node, Signal signal, Exception error, ref ExceptionDispatchInfo failure)
        {
            if (signal != Signal.Dispose) VisitEnd(node, signal, error, ref failure);
            VisitEnd(node, Signal.Dispose, null, ref failure);
        }

        private static void VisitEnd(Node node, Signal signal, Exception error, ref ExceptionDispatchInfo failure)
        {
            for (var sub = node.Subscriptions.First; sub != null; sub = sub.Next)
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
            for (var child = node.Children.First; child != null; child = child.Next)
            {
                if (child.Parent != null) VisitEnd(child, signal, error, ref failure);
            }
        }

        private enum Signal { Dispose, Completed, Error }

        // Graph storage
        private sealed class Node : Link<Node>
        {
            internal Node Parent;
            internal Chain<Node> Children;
            internal Chain<Subscription> Subscriptions;
            internal EventPath<T> Path;
            internal int ChildCount;
            internal Dictionary<Filter<T>, Node> WhereIndex;
            internal bool SkipFinished;
            internal int Remaining;
        }

        private sealed class Subscription : Link<Subscription>
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

        private abstract class Link<TLink> where TLink : Link<TLink>
        {
            internal long Registration;
            internal TLink Previous;
            internal TLink Next;
        }

        private struct Chain<TLink> where TLink : Link<TLink>
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
