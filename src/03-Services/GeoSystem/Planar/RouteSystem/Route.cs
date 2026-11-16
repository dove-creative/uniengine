using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Core;
using BlackThunder.GeoSystem.Geometry;
using BlackThunder.UniEngine.Mathematics;
using BlackThunder.GeoSystem.Navigation;
using BlackThunder.UniEngine.Objects;
using INode = BlackThunder.GeoSystem.Geometry.Planar.INode;
using ISegment = BlackThunder.GeoSystem.Geometry.Planar.ISegment;

namespace BlackThunder.GeoSystem.RouteSystem.Planar
{
    public class Route : IRoute, ISegment
    {
        // Front
        public IReadOnlyList<ISegmentView> Views { get; }
        public INode From => _views.Count == 0 ? null
            : (INode)(_views[0].IsForward ? _views[0].Source.From : _views[0].Source.To);
        public INode To => _views.Count == 0 ? null
            : (INode)(_views[_views.Count - 1].IsForward
                ? _views[_views.Count - 1].Source.To : _views[_views.Count - 1].Source.From);
        public IEnumerable<INode> Nodes
        {
            get
            {
                var nodes = new HashSet<INode>();
                foreach (var view in _views)
                {
                    foreach (INode node in view.Source.Nodes)
                        if (nodes.Add(node)) yield return node;
                }
            }
        }
        public bool IsIsolated => _views.Count == 0;
        public float Length { get; private set; }
        public float Epsilon
        {
            get => _epsilon;
            set
            {
                ThrowIfDisposed();
                Location.ValidateEpsilon(value);
                if (_epsilon == value) return;
                _epsilon = value;
                Notify((int)GeoEvents.Shape);
            }
        }

        // Property
        public IEventHub<int> EventHub => _eventHub;
        public bool IsDisposed => _disposeRequested || _eventHub.IsDisposed;

        // Internal
        private readonly List<ISegmentView> _views = new();
        private readonly Dictionary<ISegment, int> _sources = new(new SourceComparer());
        private readonly EventHub<int> _eventHub;
        private bool _disposeRequested;
        private float _epsilon = UniMath.Epsilon;

        private class SourceComparer : IEqualityComparer<ISegment>
        {
            public bool Equals(ISegment x, ISegment y) => ReferenceEquals(x, y);
            public int GetHashCode(ISegment obj) => RuntimeHelpers.GetHashCode(obj);
        }


        // Content
        public Route()
        {
            Views = _views.AsReadOnly();
            _eventHub = new EventHub<int>(this);
        }

        public void Append(ISegmentView view, bool toTail = true)
        {
            ThrowIfDisposed();
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (view.Source is not ISegment source || source.IsDisposed)
                throw new ArgumentException("A view must reference a live planar segment.", nameof(view));

            Geometry.Planar.GeoDependency.Validate(this, source);

            if (_sources.TryGetValue(source, out var count)) _sources[source] = count + 1;
            else
            {
                source.EventHub.Connect(this)
                    .OnUpdatedWhere(events => (events & (int)GeoEvents.All) != 0, () =>
                    {
                        UpdateLength();
                        Notify((int)GeoEvents.Shape);
                    })
                    .OnDisposing(Dispose);
                _sources.Add(source, 1);
            }

            var stored = new SegmentView(source, view.IsForward);
            if (toTail) _views.Add(stored);
            else _views.Insert(0, stored);
            UpdateLength();
            Notify((int)(GeoEvents.Connect | GeoEvents.Shape));
        }

        public void Remove(Geometry.ISegment segment)
        {
            ThrowIfDisposed();
            if (_views.Count == 0) return;
            var index = ReferenceEquals(_views[0].Source, segment) ? 0
                : ReferenceEquals(_views[_views.Count - 1].Source, segment) ? _views.Count - 1 : -1;
            if (index < 0) return;

            var source = (ISegment)_views[index].Source;
            _views.RemoveAt(index);
            if (_sources[source] == 1)
            {
                _sources.Remove(source);
                source.EventHub.Disconnect(this);
            }
            else _sources[source]--;

            UpdateLength();
            Notify((int)(GeoEvents.Disconnect | GeoEvents.Shape));
        }

        private void UpdateLength()
        {
            double length = 0;
            foreach (var view in _views) length += view.Source.Length;
            Length = (float)length;
        }

        public bool TryGetPosition(Location location, out Vector2 position, float? epsilon = null)
        {
            var success = TryGetTransform(location, out var transform, epsilon);
            position = success ? transform.Position : default;
            return success;
        }

        public bool TryGetTransform(Location location, out Transform2D transform, float? epsilon = null)
        {
            ThrowIfDisposed();
            var tolerance = epsilon ?? Epsilon;
            Location.ValidateEpsilon(tolerance);
            transform = default;
            if (Length <= tolerance || !location.TryResolve(Length, tolerance, out var distance)) return false;

            var remaining = location.IsForward ? distance : Length - distance;
            for (int offset = 0; offset < _views.Count; offset++)
            {
                var index = location.IsForward ? offset : _views.Count - 1 - offset;
                var view = _views[index];
                var source = (ISegment)view.Source;
                if (source.Length <= 0f) continue;
                if (remaining > (double)source.Length + tolerance)
                {
                    remaining -= source.Length;
                    continue;
                }

                return source.TryGetTransform(
                    new Location(remaining, DistanceType.Length, view.IsForward == location.IsForward),
                    out transform, tolerance);
            }

            return false;
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

        public void ConnectNode(ConnectionSide side, INavNode node)
        {
            ThrowIfDisposed();
            throw new NotImplementedException("Route endpoint editing requires a pathfinder.");
        }

        public void DisconnectNode(ConnectionSide side)
        {
            ThrowIfDisposed();
            throw new NotImplementedException("Route endpoint editing requires a pathfinder.");
        }

        public void DisconnectNode(INavNode node)
        {
            ThrowIfDisposed();
            throw new NotImplementedException("Route endpoint editing requires a pathfinder.");
        }

        public void ClearNodes() => Clear();
        public void Clear()
        {
            foreach (var source in _sources.Keys) source.EventHub.Disconnect(this);
            var changed = _views.Count != 0;
            _sources.Clear();
            _views.Clear();
            Length = 0f;
            if (changed) Notify((int)(GeoEvents.Disconnect | GeoEvents.Shape));
        }

        private void Notify(int events)
        {
            if (!IsDisposed) _eventHub.Update(events);
        }

        private void ThrowIfDisposed()
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
