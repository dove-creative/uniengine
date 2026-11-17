using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.GeoSystem.Navigation
{
    public class NavSegment : INavSegment
    {
        // Front
        public INavNode From => _from;
        public INavNode To => _to;
        public IEnumerable<INavNode> Nodes
        {
            get
            {
                if (_from != null) yield return _from;
                if (_to != null) yield return _to;
            }
        }
        public bool IsIsolated => _from == null && _to == null;
        public float Length { get; protected set; }

        // Property
        public Property Property => _property ??= new();
        public bool IsDisposed => _disposeRequested || EventHubInstance.IsDisposed;
        public IEventHub<int> EventHub => EventHubInstance;
        protected EventHub<int> EventHubInstance { get; }

        // Control
        public TraversalOptions TraversalOptions
        {
            get => _traversalOptions;
            set
            {
                ThrowIfDisposed();
                if ((value & ~TraversalOptions.Both) != 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (_traversalOptions == value) return;
                _traversalOptions = value;
                OnUpdated((int)GeoEvents.Traversal);
            }
        }

        // Internal
        private Property _property;
        private TraversalOptions _traversalOptions = TraversalOptions.Both;
        private INavNode _from;
        private INavNode _to;
        private bool _disposeRequested;


        // Content
        public NavSegment(float length = 1f)
        {
            if (float.IsNaN(length) || float.IsInfinity(length) || length < 0f)
                throw new ArgumentOutOfRangeException(nameof(length));

            Length = length;
            EventHubInstance = new EventHub<int>(this);
        }

        public NavSegment(INavNode from, INavNode to, float length = 1f) : this(length)
        {
            try
            {
                if (from != null) ConnectNode(ConnectionSide.From, from);
                if (to != null) ConnectNode(ConnectionSide.To, to);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void ConnectNode(ConnectionSide side, INavNode node)
        {
            ThrowIfDisposed();
            if (side != ConnectionSide.From && side != ConnectionSide.To)
                throw new ArgumentOutOfRangeException(nameof(side));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.IsDisposed) throw new ArgumentException("The node is disposed.", nameof(node));
            if (node is not INavNodeConnections connections)
                throw new ArgumentException("The node must support internal navigation connections.", nameof(node));

            var current = side == ConnectionSide.From ? _from : _to;
            var other = side == ConnectionSide.From ? _to : _from;
            if (ReferenceEquals(current, node)) return;
            if (current != null) throw new InvalidOperationException("The endpoint is already occupied.");
            if (ReferenceEquals(other, node))
                throw new ArgumentException("A segment cannot connect both ends to the same node.", nameof(node));

            OnNodeConnecting(side, node);
            connections.ValidateSegment(this);
            connections.ConnectSegmentInternal(this);
            if (side == ConnectionSide.From) _from = node;
            else _to = node;

            int events;
            try
            {
                events = OnNodeConnected(node);
            }
            catch
            {
                if (side == ConnectionSide.From) _from = null;
                else _to = null;
                connections.DisconnectSegmentInternal(this);
                OnNodeDisconnected(node);
                throw;
            }

            try { connections.NotifyConnectionChanged((int)GeoEvents.Connect); }
            finally { OnUpdated((int)GeoEvents.Connect | events); }
        }

        public void DisconnectNode(ConnectionSide side)
        {
            if (side != ConnectionSide.From && side != ConnectionSide.To)
                throw new ArgumentOutOfRangeException(nameof(side));
            DisconnectNode(side == ConnectionSide.From ? _from : _to);
        }

        public void DisconnectNode(INavNode node)
        {
            if (node == null) return;
            if (!ReferenceEquals(_from, node) && !ReferenceEquals(_to, node)) return;

            var connections = (INavNodeConnections)node;
            if (ReferenceEquals(_from, node)) _from = null;
            else _to = null;
            connections.DisconnectSegmentInternal(this);
            var events = OnNodeDisconnected(node);

            try { connections.NotifyConnectionChanged((int)GeoEvents.Disconnect); }
            finally { OnUpdated((int)GeoEvents.Disconnect | events); }
        }

        public void ClearNodes()
        {
            try { DisconnectNode(ConnectionSide.From); }
            finally { DisconnectNode(ConnectionSide.To); }
        }

        public bool IsFromNode(IReadOnlyNavNode node)
        {
            ThrowIfDisposed();
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (ReferenceEquals(node, _from)) return true;
            if (ReferenceEquals(node, _to)) return false;
            throw new ArgumentException("The node is not part of this segment.", nameof(node));
        }

        public IReadOnlyNavNode GetCounterNode(IReadOnlyNavNode node) => IsFromNode(node) ? _to : _from;
        public INavNode GetCounterNode(INavNode node) => IsFromNode(node) ? _to : _from;

        protected virtual void OnNodeConnecting(ConnectionSide side, INavNode node) { }
        protected virtual int OnNodeConnected(INavNode node) => 0;
        protected virtual int OnNodeDisconnected(INavNode node) => 0;
        protected virtual void OnDisposing() { }

        protected void OnUpdated(int events)
        {
            if (!IsDisposed) EventHubInstance.Update(events);
        }

        protected void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            _disposeRequested = true;
            try { OnDisposing(); }
            finally
            {
                try { ClearNodes(); }
                finally { EventHubInstance.Dispose(); }
            }
        }

        public string FormatLogMessage(string message) => $"[{GetType().Name}] {message}";
    }
}
