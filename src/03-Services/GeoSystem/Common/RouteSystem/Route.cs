using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.GeoSystem.Navigation.Pathfinders;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.GeoSystem.RouteSystem
{
    public class Route : IRoute
    {
        // Front
        public IReadOnlyList<ISegmentView> Views { get; }
        public INode From => _views.Count == 0 ? _from
            : (_views[0].IsForward ? _views[0].Source.From : _views[0].Source.To);
        public INode To => _views.Count == 0 ? _to
            : (_views[_views.Count - 1].IsForward
                ? _views[_views.Count - 1].Source.To : _views[_views.Count - 1].Source.From);
        public IEnumerable<INode> Nodes
        {
            get
            {
                var nodes = new HashSet<INode>(new ReferenceComparer<INode>());
                if (_views.Count == 0)
                {
                    if (_from != null && nodes.Add(_from)) yield return _from;
                    if (_to != null && nodes.Add(_to)) yield return _to;
                }
                else
                {
                    foreach (var view in _views)
                        foreach (var node in view.Source.Nodes)
                            if (node != null && nodes.Add(node)) yield return node;
                }
            }
        }
        public bool IsIsolated => _views.Count == 0 && _from == null && _to == null;
        public float Length { get; private set; }

        // Property
        public Property Property => _property ??= new();
        public IEventHub<int> EventHub => _eventHub;
        public bool IsDisposed => _disposeRequested || _eventHub.IsDisposed;

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
                Notify((int)GeoEvents.Traversal);
            }
        }

        // Internal
        private readonly IPathfinder _pathfinder;
        private readonly EventHub<int> _eventHub;
        private readonly List<ISegmentView> _views = new();
        private Dictionary<ISegment, int> _sources = new(new ReferenceComparer<ISegment>());
        private HashSet<INode> _anchors = new(new ReferenceComparer<INode>());
        private readonly object _anchorListener = new();
        private INode _from;
        private INode _to;
        private Property _property;
        private TraversalOptions _traversalOptions = TraversalOptions.Both;
        private bool _disposeRequested;

        private class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private readonly struct StoredView : ISegmentView
        {
            public ISegment Source { get; }
            public bool IsForward { get; }


            public StoredView(ISegment source, bool isForward)
            {
                Source = source;
                IsForward = isForward;
            }
        }


        // Content --------------------
        // Construction
        public Route() : this(new Dijkstra()) { }
        public Route(IPathfinder pathfinder)
        {
            _pathfinder = pathfinder ?? throw new ArgumentNullException(nameof(pathfinder));
            Views = _views.AsReadOnly();
            _eventHub = new EventHub<int>(this);
        }

        // Endpoints
        public bool TryConnectNode(ConnectionSide side, INavNode node)
        {
            ThrowIfDisposed();
            ValidateSide(side);
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node is not INode endpoint)
                throw new ArgumentException("A route requires a geometry node.", nameof(node));
            ValidateNode(endpoint);

            var current = side == ConnectionSide.From ? From : To;
            if (ReferenceEquals(current, node)) return true;
            var from = side == ConnectionSide.From ? endpoint : From;
            var to = side == ConnectionSide.To ? endpoint : To;
            if (from != null) ValidateNode(from);
            if (to != null) ValidateNode(to);

            var views = new List<ISegmentView>();
            if (from != null && to != null && !ReferenceEquals(from, to))
            {
                if (!_pathfinder.TryPathfind(from, new[] { to }, out var path)) return false;
                views = ValidatePath(path, from, to);
            }

            var events = GeoEvents.Connect | GeoEvents.Shape;
            if (current != null || _views.Count != 0) events |= GeoEvents.Disconnect;
            Replace(views, from, to, (int)events);
            return true;
        }

        public void ConnectNode(ConnectionSide side, INavNode node)
        {
            if (!TryConnectNode(side, node))
                throw new InvalidOperationException("No traversable path connects the requested route endpoints.");
        }

        public void DisconnectNode(ConnectionSide side)
        {
            ThrowIfDisposed();
            ValidateSide(side);
            if ((side == ConnectionSide.From ? From : To) == null) return;
            Replace(
                new List<ISegmentView>(),
                side == ConnectionSide.From ? null : From,
                side == ConnectionSide.To ? null : To,
                (int)(GeoEvents.Disconnect | GeoEvents.Shape));
        }

        public void DisconnectNode(INavNode node)
        {
            ThrowIfDisposed();
            if (node == null) return;
            var from = From;
            var to = To;
            if (!ReferenceEquals(from, node) && !ReferenceEquals(to, node)) return;
            Replace(
                new List<ISegmentView>(),
                ReferenceEquals(from, node) ? null : from,
                ReferenceEquals(to, node) ? null : to,
                (int)(GeoEvents.Disconnect | GeoEvents.Shape));
        }

        public bool IsFromNode(IReadOnlyNavNode node)
        {
            ThrowIfDisposed();
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (ReferenceEquals(node, From)) return true;
            if (ReferenceEquals(node, To)) return false;
            throw new ArgumentException("The node is not an endpoint of this route.", nameof(node));
        }

        public IReadOnlyNavNode GetCounterNode(IReadOnlyNavNode node) => IsFromNode(node) ? To : From;

        private void ValidateSide(ConnectionSide side)
        {
            if (side != ConnectionSide.From && side != ConnectionSide.To)
                throw new ArgumentOutOfRangeException(nameof(side));
        }

        protected virtual void ValidateNode(INode node)
        {
            if (node.IsDisposed) throw new ArgumentException("The node is disposed.", nameof(node));
            GeoDependency.Validate(this, node);
        }

        protected virtual void ValidateSource(ISegment source)
        {
            if (source == null || source.IsDisposed)
                throw new ArgumentException("A view must reference a live geometry segment.", nameof(source));
            GeoDependency.Validate(this, source);
        }

        private List<ISegmentView> ValidatePath(Navigation.Path path, INode from, INode to)
        {
            if (path == null || !ReferenceEquals(path.From, from))
                throw new InvalidOperationException("The pathfinder returned an invalid source.");

            var views = new List<ISegmentView>();
            var current = from;
            foreach (var arrow in path.Arrows)
            {
                if (arrow.Segment is not ISegment source || arrow.To is not INode next)
                    throw new InvalidOperationException("The path must contain geometry segments and nodes.");
                ValidateSource(source);
                ValidateNode(next);
                var forward = ReferenceEquals(source.From, current) && ReferenceEquals(source.To, next);
                var backward = ReferenceEquals(source.To, current) && ReferenceEquals(source.From, next);
                if ((!forward && !backward) || ReferenceEquals(current, next))
                    throw new InvalidOperationException("The pathfinder returned a discontinuous path.");
                var direction = forward ? TraversalOptions.Forward : TraversalOptions.Backward;
                if ((source.TraversalOptions & direction) == 0)
                    throw new InvalidOperationException("The pathfinder returned a prohibited traversal.");
                views.Add(new StoredView(source, forward));
                current = next;
            }

            if (!ReferenceEquals(current, to))
                throw new InvalidOperationException("The pathfinder returned an unexpected destination.");
            return views;
        }

        // Views
        public void Append(ISegmentView view, bool toTail = true)
        {
            ThrowIfDisposed();
            if (view == null) throw new ArgumentNullException(nameof(view));
            var source = view.Source;
            ValidateSource(source);
            var views = new List<ISegmentView>(_views);
            var stored = new StoredView(source, view.IsForward);
            if (toTail) views.Add(stored);
            else views.Insert(0, stored);
            Replace(views, null, null, (int)(GeoEvents.Connect | GeoEvents.Shape));
        }

        public void Remove(ISegment segment)
        {
            ThrowIfDisposed();
            if (_views.Count == 0) return;
            var index = ReferenceEquals(_views[0].Source, segment) ? 0
                : ReferenceEquals(_views[_views.Count - 1].Source, segment) ? _views.Count - 1 : -1;
            if (index < 0) return;
            var views = new List<ISegmentView>(_views);
            views.RemoveAt(index);
            Replace(views, null, null, (int)(GeoEvents.Disconnect | GeoEvents.Shape));
        }

        private void Replace(List<ISegmentView> views, INode from, INode to, int events)
        {
            var sources = new Dictionary<ISegment, int>(new ReferenceComparer<ISegment>());
            double length = 0;
            foreach (var view in views)
            {
                sources.TryGetValue(view.Source, out var count);
                sources[view.Source] = count + 1;
                length += view.Source.Length;
            }

            var anchors = new HashSet<INode>(new ReferenceComparer<INode>());
            if (views.Count == 0)
            {
                if (from != null) anchors.Add(from);
                if (to != null) anchors.Add(to);
            }

            var addedSources = new List<ISegment>();
            var addedAnchors = new List<INode>();
            try
            {
                foreach (var source in sources.Keys)
                {
                    if (_sources.ContainsKey(source)) continue;
                    var builder = source.EventHub.Connect(this);
                    addedSources.Add(source);
                    builder.OnUpdatedWhere(
                        flags => (flags & (int)(GeoEvents.AnyConnection | GeoEvents.Shape)) != 0,
                        UpdateShape).OnDisposing(Dispose);
                }

                foreach (var anchor in anchors)
                {
                    if (_anchors.Contains(anchor)) continue;
                    var builder = anchor.EventHub.Connect(_anchorListener);
                    addedAnchors.Add(anchor);
                    builder.OnDisposing(Dispose);
                }
            }
            catch
            {
                foreach (var source in addedSources) source.EventHub.Disconnect(this);
                foreach (var anchor in addedAnchors) anchor.EventHub.Disconnect(_anchorListener);
                throw;
            }

            foreach (var source in _sources.Keys)
                if (!sources.ContainsKey(source)) source.EventHub.Disconnect(this);
            foreach (var anchor in _anchors)
                if (!anchors.Contains(anchor)) anchor.EventHub.Disconnect(_anchorListener);

            _sources = sources;
            _anchors = anchors;
            _views.Clear();
            _views.AddRange(views);
            _from = views.Count == 0 ? from : null;
            _to = views.Count == 0 ? to : null;
            Length = (float)length;
            Notify(events);
        }

        private void UpdateShape()
        {
            double length = 0;
            foreach (var view in _views) length += view.Source.Length;
            Length = (float)length;
            Notify((int)GeoEvents.Shape);
        }

        // Lifetime
        public void ClearNodes() => Clear();
        public void Clear()
        {
            if (IsIsolated) return;
            Replace(new List<ISegmentView>(), null, null, (int)(GeoEvents.Disconnect | GeoEvents.Shape));
        }

        protected void Notify(int events)
        {
            if (!IsDisposed) _eventHub.Update(events);
        }

        protected void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            _disposeRequested = true;
            try { Clear(); }
            finally { _eventHub.Dispose(); }
        }

        public string FormatLogMessage(string message) => $"[Route] {message}";
    }
}
