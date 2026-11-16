using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.GeoSystem.Navigation
{
    public class NavNode : INavNode, INavNodeConnections
    {
        // Front
        public IReadOnlyList<INavSegment> Segments { get; }
        public bool IsIsolated => _segments.Count == 0;
        IEnumerable<INavSegment> INavNode.Segments => Segments;

        // Property
        public bool IsDisposed => _disposeRequested || EventHubInstance.IsDisposed;
        public IEventHub<int> EventHub => EventHubInstance;
        protected EventHub<int> EventHubInstance { get; }

        // Internal
        private readonly List<INavSegment> _segments = new();
        private bool _disposeRequested;


        // Content
        public NavNode()
        {
            Segments = _segments.AsReadOnly();
            EventHubInstance = new EventHub<int>(this);
        }

        public void ConnectSegment(INavSegment segment, ConnectionSide side)
        {
            ThrowIfDisposed();
            if (segment == null) throw new ArgumentNullException(nameof(segment));
            if (_segments.Any(item => ReferenceEquals(item, segment)))
                throw new ArgumentException("The segment is already connected.", nameof(segment));

            segment.ConnectNode(side, this);
        }

        public void DisconnectSegment(INavSegment segment)
        {
            if (segment == null || !_segments.Any(item => ReferenceEquals(item, segment))) return;
            segment.DisconnectNode(this);
        }

        public void ClearSegments()
        {
            List<Exception> errors = null;
            foreach (var segment in _segments.ToArray())
            {
                try { segment.DisconnectNode(this); }
                catch (Exception error) { (errors ??= new()).Add(error); }
            }
            if (errors != null) throw new AggregateException(errors);
        }

        void INavNodeConnections.ValidateSegment(INavSegment segment)
        {
            ThrowIfDisposed();
            OnSegmentConnecting(segment);
        }

        void INavNodeConnections.ConnectSegmentInternal(INavSegment segment) => _segments.Add(segment);
        void INavNodeConnections.DisconnectSegmentInternal(INavSegment segment) =>
            _segments.RemoveAll(item => ReferenceEquals(item, segment));
        void INavNodeConnections.NotifyConnectionChanged(int events) => OnUpdated(events);

        protected virtual void OnSegmentConnecting(INavSegment segment) { }
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
            try
            {
                OnDisposing();
            }
            finally
            {
                try { ClearSegments(); }
                finally { EventHubInstance.Dispose(); }
            }
        }

        public string FormatLogMessage(string message) => $"[{GetType().Name}] {message}";
    }
}
