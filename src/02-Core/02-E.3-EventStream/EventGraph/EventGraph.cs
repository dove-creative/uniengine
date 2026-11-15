using System;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.Core
{
    internal partial class EventGraph<T> : IDisposable
    {
        // Front
        internal int NodeCount { get; private set; }
        internal bool IsPublishing { get; private set; }
        internal EventDispatchScope Scope { get; } = new();

        // Internal
        private const int RowIndexThreshold = 16;

        // Only ulong paths create flags nodes; the delegate cast does not box published values.
        private static readonly Func<T, ulong> _readFlags = typeof(T) == typeof(ulong)
            ? (Func<T, ulong>)(object)new Func<ulong, ulong>(bits => bits)
            : null;

        private Node _root = new();
        private readonly EventDispatchScope _terminalScope = new();
        private readonly Action _onEmpty;
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
        internal Connection BeginRegistration()
        {
            if (_closed) throw new ObjectDisposedException("EventStream");

            _lastRegistration++;
            return _root.Connection;
        }

        internal Handle Subscribe(
            Connection node,
            Action<T> updated,
            Action completed,
            Action<Exception> errored,
            IGraphObserver<T> input = null,
            bool sequential = false,
            EventSubscriptionHandle handle = null) =>
            Attach(FindGate(node), updated, completed, errored, input, sequential, handle);

        internal Handle Connect(Connection node, IGraphObserver<T> input, EventSubscriptionHandle handle = null)
        {
            var gate = AddChild(node, new OutputGate(isConnection: true));
            return Attach(gate, input.OnNext, input.OnCompleted, input.OnError, input, handle: handle);
        }

        // Path steps return the connection owned by a row or a keyless node.
        internal Connection FindOrAddGroup(Connection parent, int order, bool parallel = false)
        {
            var node = Find<OrderedNode>(parent) ?? AddChild(parent, new OrderedNode());
            return node.FindOrAdd(order, parallel, _lastRegistration);
        }

        internal Connection FindOrAddFlags(Connection parent, ulong mask)
        {
            var node = Find<FlagsNode>(parent) ?? AddChild(parent, new FlagsNode());
            return node.FindOrAdd(mask, _lastRegistration);
        }

        internal Connection FindOrAddWhere(Connection parent, Func<T, bool> predicate)
        {
            var node = Find<WhereNode>(parent) ?? AddChild(parent, new WhereNode());
            return node.FindOrAdd(predicate, _lastRegistration);
        }

        internal Connection FindOrAddWhen(Connection parent, T target)
        {
            var node = Find<WhenNode>(parent) ?? new WhenNode();
            // Compare before attaching a new node, so failed registration leaves no empty child.
            var connection = node.FindOrAdd(target, _lastRegistration);
            if (node.Parent == null) AddChild(parent, node);
            return connection;
        }

        internal Connection AddDebug(Connection parent, string message, LogLevel logType) =>
            AddChild(parent, new DebugNode(message, logType)).Connection;

        // Read ownership without creating nodes or promoting indexes during propagation.
        internal EventDispatchScope RegistrationScope(PathInfo<T> path)
        {
            var connection = _root.Connection;
            var scope = Scope;
            for (var info = path; info != null; info = info.Next)
            {
                switch (info.NodeType)
                {
                    case PathNodeType.Group:
                    case PathNodeType.ParallelGroup:
                        var ordered = Find<OrderedNode>(connection);
                        var key = (long)info.Order * 2;
                        var existing = ordered?.Find(key) ?? ordered?.Find(key + 1);
                        if (existing == null) return null;
                        scope = existing.Scope;
                        connection = ordered.Find(key + (info.NodeType == PathNodeType.Group ? 1 : 0));
                        break;
                    case PathNodeType.Where:
                        connection = Find<WhereNode>(connection)?.Find(info.Predicate);
                        break;
                    case PathNodeType.When:
                        connection = Find<WhenNode>(connection)?.FindConnection(info.Target);
                        break;
                    case PathNodeType.Flags:
                        connection = Find<FlagsNode>(connection)?.Find(info.Mask);
                        break;
                    default:
                        return scope;
                }
                if (connection == null) return scope;
            }
            return scope;
        }

        private static TNode Find<TNode>(Connection parent) where TNode : Node
        {
            for (var child = parent.Children.First; child != null; child = child.Next)
            {
                // Pending registrations must not enter a When index scheduled for cleanup.
                if (child is TNode node && !(node is WhenNode { IsClosing: true })) return node;
            }

            return null;
        }

        // The shared gate is the first child; stream connections retain their creation order.
        private OutputGate FindGate(Connection connection) =>
            connection.Children.First is OutputGate { IsConnection: false } gate
                ? gate
                : AddChild(connection, new OutputGate(isConnection: false), connection.Children.First);

        private TChild AddChild<TChild>(Connection parent, TChild child, NodeChild before = null)
            where TChild : NodeChild
        {
            child.Registration = _lastRegistration;
            child.Parent = parent;
            child.Scope = parent.Scope;
            parent.Children.Insert(child, before);

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

        internal void CancelRegistration(Connection connection)
        {
            if (connection.IsEmpty)
            {
                connection.Owner.Remove();
                Prune(connection.Owner.Node);
            }

            if (!_closed && _root.IsEmpty) _onEmpty?.Invoke();
        }

        private void Prune(NodeChild child)
        {
            while (child.Parent != null && child.IsEmpty)
            {
                var parent = child.Parent;
                parent.Children.Remove(child);
                child.Parent = null;

                if (CountsAsNode(child)) NodeCount--;
                if (!parent.IsEmpty) return;

                parent.Owner.Remove();
                child = parent.Owner.Node;
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
