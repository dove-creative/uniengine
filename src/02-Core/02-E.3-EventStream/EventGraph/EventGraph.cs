using System;
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
        private const int RowIndexThreshold = 16;

        // Only ulong paths create flags nodes; the delegate cast does not box published values.
        private static readonly Func<T, ulong> _readFlags = typeof(T) == typeof(ulong)
            ? (Func<T, ulong>)(object)new Func<ulong, ulong>(bits => bits)
            : null;

        private Node _root = new();
        private readonly Action _onEmpty;
        private long _lastRegistration;
        private bool _closed;


        // Content --------------------
        // Registration
        internal EventGraph(Action onEmpty) => _onEmpty = onEmpty;

        // An operator stream replaces the root while it is built, before anything can subscribe.
        internal void Inject(Node root) => _root = root;

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
            IGraphObserver<T> input = null) =>
            Attach(FindGate(node), updated, completed, errored, input);

        internal Handle Connect(Connection node, IGraphObserver<T> input)
        {
            var gate = AddChild(node, new OutputGate(isConnection: true));
            return Attach(gate, input.OnNext, input.OnCompleted, input.OnError, input);
        }

        // Path steps return the connection owned by a row or a keyless node.
        internal Connection FindOrAddGroup(Connection parent, int order)
        {
            var node = Find<OrderedNode>(parent) ?? AddChild(parent, new OrderedNode());
            return node.FindOrAdd(order, _lastRegistration);
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

        private static TNode Find<TNode>(Connection parent) where TNode : Node
        {
            for (var child = parent.Children.First; child != null; child = child.Next)
            {
                if (child is TNode node) return node;
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
