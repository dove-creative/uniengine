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
        internal EventDispatchScope Scope { get; } = new();

        // Internal
        private const int WhereIndexThreshold = 16;

        // Only ulong paths create flags nodes; the delegate cast does not box published values.
        private static readonly Func<T, ulong> _readFlags = typeof(T) == typeof(ulong)
            ? (Func<T, ulong>)(object)new Func<ulong, ulong>(bits => bits)
            : null;

        private Node _root = new();
        private readonly EventDispatchScope _terminalScope = new();
        private readonly Action _onEmpty;
        // Only wide parents get an index; routing nodes keep their existing size and layout.
        private Dictionary<Node, Dictionary<Func<T, bool>, WhereNode>> _whereIndexes;
        private long _lastRegistration;
        private bool _closed;


        // Content --------------------
        // Registration
        internal EventGraph(Action onEmpty)
        {
            _onEmpty = onEmpty;
            _root.Scope = Scope;
        }

        // An operator stream replaces the root while it is built, before anything can subscribe.
        internal void Inject(Node root)
        {
            _root = root;
            _root.Scope = Scope;
        }

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
            IGraphObserver<T> input = null,
            bool sequential = false,
            EventSubscriptionHandle handle = null) =>
            Attach(FindGate(node), updated, completed, errored, input, sequential, handle);

        internal Handle Connect(Node node, IGraphObserver<T> input, EventSubscriptionHandle handle = null)
        {
            var gate = AddChild(node, new OutputGate(isConnection: true));
            return Attach(gate, input.OnNext, input.OnCompleted, input.OnError, input, handle: handle);
        }

        // Node operations for paths
        internal Node FindOrAddGroup(Node parent, int order, bool parallel = false)
        {
            var ordered = FindOrdered(parent) ?? AddChild(parent, new OrderedNode());
            var key = (long)order * 2 + (parallel ? 0 : 1);
            var group = ordered.Find(key);

            if (group != null) return group;

            group = AddChild(ordered, parallel ? new ParallelGroup() : new Node(), ordered.FindNext(key));
            group.Scope = ordered.Find(key ^ 1)?.Scope ?? new EventDispatchScope(Scope);
            ordered.Add(key, group);

            return group;
        }

        // Resolve ownership without creating nodes during an active publication.
        internal EventDispatchScope RegistrationScope(PathInfo<T> path)
        {
            var node = _root;
            var scope = Scope;
            for (var info = path; info != null; info = info.Next)
            {
                if (info.NodeType == PathNodeType.Group || info.NodeType == PathNodeType.ParallelGroup)
                {
                    var ordered = FindOrdered(node);
                    var key = (long)info.Order * 2;
                    var existing = ordered?.Find(key) ?? ordered?.Find(key + 1);
                    // A new numeric order is not an active ancestor.
                    if (existing == null) return null;
                    scope = existing.Scope;
                    node = ordered.Find(key + (info.NodeType == PathNodeType.Group ? 1 : 0));
                }
                else if (info.NodeType == PathNodeType.Where)
                    node = FindWhere(node, info.Predicate, indexWideParent: false);
                else if (info.NodeType == PathNodeType.Flags)
                    node = FindFlags(node)?.Find(info.Mask);
                else
                    return scope; // Debug always creates a new path.

                if (node == null) return scope;
            }
            return scope;
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

        internal Node FindOrAddFlags(Node parent, ulong mask)
        {
            var flags = FindFlags(parent) ?? AddChild(parent, new FlagsNode());
            return flags.Find(mask) ?? AddChild(flags, new FlagsEntry(mask));
        }

        // The masks of a position share one flags node, in creation order.
        private static FlagsNode FindFlags(Node parent)
        {
            for (var child = parent.Children.First; child != null; child = child.Next)
            {
                if (child is FlagsNode flags) return flags;
            }

            return null;
        }

        internal Node FindOrAddWhere(Node parent, Func<T, bool> predicate) =>
            FindWhere(parent, predicate) ?? AddChild(parent, new WhereNode(predicate));

        internal Node AddDebug(Node parent, string message, LogLevel logType) =>
            AddChild(parent, new DebugNode(message, logType));

        // Small parents avoid hashing. Promote only when a new condition would exceed the scan limit.
        private WhereNode FindWhere(Node parent, Func<T, bool> predicate, bool indexWideParent = true)
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

            if (indexWideParent && count >= WhereIndexThreshold) IndexWhere(parent, count);

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
            child.Scope = parent.Scope;
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
            IGraphObserver<T> input,
            bool sequential = false,
            EventSubscriptionHandle handle = null)
        {
            var subscription = new Subscription(updated, completed, errored, input != null, sequential)
            {
                Registration = _lastRegistration
            };

            handle ??= new EventSubscriptionHandle(gate.Scope);
            subscription.Handle = handle;
            handle.Bind(gate.Scope, () =>
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
            Scope.Enter();
            ExceptionDispatchInfo failure = null;

            try
            {
                _root.Visit(value, _lastRegistration, ref failure);
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                try { Scope.Exit(ref failure); }
                finally { IsPublishing = false; }
            }
            failure?.Throw();
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
            _terminalScope.Enter();
            try
            {
                _root.VisitEnd(completed ? Signal.Completed : Signal.Error, error, ref failure);
            }
            finally
            {
                _terminalScope.Exit(ref failure);
                _root.VisitEnd(Signal.Dispose, null, ref failure);
            }
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
