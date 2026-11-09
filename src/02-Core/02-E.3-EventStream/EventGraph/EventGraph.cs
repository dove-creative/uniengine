using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T> : IDisposable
    {
        // Front
        internal int NodeCount { get; private set; }
        internal bool IsPublishing { get; private set; }

        // Internal
        private const int WhereIndexThreshold = 16;

        private Node _root = new();
        private readonly Action _onEmpty;
        // Only wide parents get an index; routing nodes keep their existing size and layout.
        private Dictionary<Node, Dictionary<Func<T, bool>, WhereNode>> _whereIndexes;
        private long _lastRegistration;
        private bool _closed;


        // Content --------------------
        // Registration
        internal EventGraph(Action onEmpty) => _onEmpty = onEmpty;

        // An operator stream replaces the root while it is built, before anything can subscribe.
        internal void Inject(Node root) => _root = root;

        // Starts a registration at the root. Nodes added from here on stay invisible
        // to a publication that is already running.
        internal Node BeginRegistration()
        {
            if (_closed) throw new ObjectDisposedException("EventStream");

            _lastRegistration++;
            return _root;
        }

        internal Handle Subscribe(
            Node node,
            Action<T> updated,
            Action completed,
            Action<Exception> errored,
            IGraphObserver<T> input = null) =>
            Attach(FindGate(node), updated, completed, errored, input);

        internal Handle Connect(Node node, IGraphObserver<T> input)
        {
            var gate = AddChild(node, new OutputGate(isConnection: true));
            return Attach(gate, input.OnNext, input.OnCompleted, input.OnError, input);
        }

        // Node operations for paths
        internal Node FindOrAddGroup(Node parent, int order)
        {
            var ordered = FindOrdered(parent) ?? AddChild(parent, new OrderedNode());
            var group = ordered.Find(order);

            if (group != null) return group;

            group = AddChild(ordered, new Node(), ordered.FindNext(order));
            ordered.Add(order, group);

            return group;
        }

        // The groups of a position share one ordered node.
        private static OrderedNode FindOrdered(Node parent)
        {
            for (var child = parent.Children.First; child != null; child = child.Next)
            {
                if (child is OrderedNode ordered) return ordered;
            }

            return null;
        }

        internal Node FindOrAddWhere(Node parent, Func<T, bool> predicate) =>
            FindWhere(parent, predicate) ?? AddChild(parent, new WhereNode(predicate));

        internal Node AddDebug(Node parent, string message, LogLevel logType) =>
            AddChild(parent, new DebugNode(message, logType));

        // Small parents avoid hashing. Promote only when a new condition would exceed the scan limit.
        private WhereNode FindWhere(Node parent, Func<T, bool> predicate)
        {
            if (_whereIndexes != null && _whereIndexes.TryGetValue(parent, out var index))
                return index.TryGetValue(predicate, out var existing) ? existing : null;

            var count = 0;
            for (var child = parent.Children.First; child != null; child = child.Next)
            {
                if (!(child is WhereNode whereNode)) continue;
                if (whereNode.Predicate == predicate) return whereNode;
                count++;
            }

            if (count >= WhereIndexThreshold) IndexWhere(parent, count);

            return null;
        }

        private void IndexWhere(Node parent, int count)
        {
            var index = new Dictionary<Func<T, bool>, WhereNode>(count + 1);
            for (var child = parent.Children.First; child != null; child = child.Next)
            {
                if (child is WhereNode whereNode) index.Add(whereNode.Predicate, whereNode);
            }

            _whereIndexes ??= new Dictionary<Node, Dictionary<Func<T, bool>, WhereNode>>();
            _whereIndexes.Add(parent, index);
        }

        // Subscribers run before the children of their node, so the shared gate stays the first child.
        private OutputGate FindGate(Node node) =>
            node.Children.First is OutputGate { IsConnection: false } gate
                ? gate
                : AddChild(node, new OutputGate(isConnection: false), node.Children.First);

        private TChild AddChild<TChild>(NodeBase parent, TChild child, NodeChild before = null)
            where TChild : NodeChild
        {
            child.Registration = _lastRegistration;
            child.Parent = parent;
            parent.Children.Insert(child, before);

            if (_whereIndexes != null && child is WhereNode whereNode
                && _whereIndexes.TryGetValue((Node)parent, out var index))
                index.Add(whereNode.Predicate, whereNode);

            if (CountsAsNode(child)) NodeCount++;

            return child;
        }

        // A shared gate only holds the subscribers of its parent, so a path keeps its size with one.
        private static bool CountsAsNode(NodeChild child) => !(child is OutputGate { IsConnection: false });

        private Handle Attach(
            OutputGate gate,
            Action<T> updated,
            Action completed,
            Action<Exception> errored,
            IGraphObserver<T> input)
        {
            var subscription = new Subscription(updated, completed, errored, input != null)
            {
                Registration = _lastRegistration
            };

            subscription.Handle = new Handle(() =>
            {
                gate.Subscriptions.Remove(subscription);
                Prune(gate);
                input?.OnDisconnected();

                if (!_closed && _root.IsEmpty) _onEmpty?.Invoke();
            });

            gate.Subscriptions.Insert(subscription);

            return subscription.Handle;
        }

        private void Prune(NodeChild child)
        {
            while (child.Parent != null && child.IsEmpty)
            {
                var parent = child.Parent;
                parent.Children.Remove(child);

                if (parent is OrderedNode ordered && child is Node group) ordered.Remove(group);
                if (_whereIndexes != null && child is WhereNode whereNode
                    && _whereIndexes.TryGetValue((Node)parent, out var index))
                {
                    index.Remove(whereNode.Predicate);
                    // Keep a shrunken index until its last condition leaves, avoiding promotion churn.
                    if (index.Count == 0)
                    {
                        _whereIndexes.Remove((Node)parent);
                        if (_whereIndexes.Count == 0) _whereIndexes = null;
                    }
                }

                child.Parent = null;

                if (CountsAsNode(child)) NodeCount--;

                child = parent;
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
                _root.Visit(value, _lastRegistration, ref failure);
                failure?.Throw();
            }
            finally
            {
                IsPublishing = false;
            }
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
            _root.VisitEnd(completed ? Signal.Completed : Signal.Error, error, ref failure);
            _root.VisitEnd(Signal.Dispose, null, ref failure);
            failure?.Throw();
        }

        public void Dispose()
        {
            _closed = true;

            ExceptionDispatchInfo failure = null;
            _root.VisitEnd(Signal.Dispose, null, ref failure);
            failure?.Throw();
        }
    }
}
