using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using BlackThunder.GeoSystem.Navigation;

namespace BlackThunder.GeoSystem.Geometry
{
    public abstract class PolySegment<P, R> : Segment<P, R>, IPolySegment<P, R>
    {
        // Front
        public override IDictionary<string, float> Values => _values;
        public override IList<SegmentHandle<P>> Handles => _handles;
        public IReadOnlyList<Hinge<P, R>> Hinges { get; }

        // Control
        public IShaper<P> Shaper
        {
            get => _shaper;
            set
            {
                ThrowIfDisposed();
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (ReferenceEquals(value, _shaper)) return;
                _shaper = value;
                OnInputsChanged();
            }
        }

        // Internal
        private IShaper<P> _shaper;
        private readonly SegmentValues _values;
        private readonly HandleCollection _handles;
        private readonly List<Hinge<P, R>> _hinges = new();
        private readonly HashSet<SegmentHandle<P>> _subscribedHandles = new(new HandleComparer());

        private sealed class HandleComparer : IEqualityComparer<SegmentHandle<P>>
        {
            public bool Equals(SegmentHandle<P> x, SegmentHandle<P> y) => ReferenceEquals(x, y);
            public int GetHashCode(SegmentHandle<P> obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private class HandleCollection : Collection<SegmentHandle<P>>
        {
            private readonly PolySegment<P, R> _owner;


            public HandleCollection(PolySegment<P, R> owner) => _owner = owner;

            protected override void InsertItem(int index, SegmentHandle<P> item)
            {
                _owner.ThrowIfDisposed();
                _owner.ValidateHandle(item);
                base.InsertItem(index, item);
                _owner.OnHandlesChanged();
            }

            protected override void SetItem(int index, SegmentHandle<P> item)
            {
                _owner.ThrowIfDisposed();
                _owner.ValidateHandle(item);
                if (ReferenceEquals(this[index], item)) return;
                base.SetItem(index, item);
                _owner.OnHandlesChanged();
            }

            protected override void RemoveItem(int index)
            {
                _owner.ThrowIfDisposed();
                base.RemoveItem(index);
                _owner.OnHandlesChanged();
            }

            protected override void ClearItems()
            {
                _owner.ThrowIfDisposed();
                if (Count == 0) return;
                base.ClearItems();
                _owner.OnHandlesChanged();
            }
        }


        // Content --------------------
        // Construction
        protected PolySegment(IShaper<P> shaper)
        {
            _shaper = shaper ?? throw new ArgumentNullException(nameof(shaper));
            _values = new SegmentValues(ThrowIfDisposed, OnInputsChanged);
            _handles = new HandleCollection(this);
            Hinges = _hinges.AsReadOnly();
        }

        // Connections
        protected virtual void ValidateHandle(SegmentHandle<P> handle)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            if (handle.IsDisposed) throw new ArgumentException("The handle is disposed.", nameof(handle));
        }

        protected override int OnNodeConnected(INavNode node)
        {
            node.EventHub.Connect(this)
                .OnUpdatedWhere(events => (events & (int)GeoEvents.Shape) != 0, OnInputsChanged);
            RebuildShape();
            return (int)GeoEvents.Shape;
        }

        protected override int OnNodeDisconnected(INavNode node)
        {
            node.EventHub.Disconnect(this);
            RebuildShape();
            return (int)GeoEvents.Shape;
        }

        private void OnHandlesChanged()
        {
            var current = new HashSet<SegmentHandle<P>>(_handles, new HandleComparer());
            foreach (var handle in _subscribedHandles.ToArray())
            {
                if (current.Contains(handle)) continue;
                handle.EventHub.Disconnect(this);
                _subscribedHandles.Remove(handle);
            }
            foreach (var handle in current)
            {
                if (!_subscribedHandles.Add(handle)) continue;
                handle.EventHub.Connect(this)
                    .OnUpdatedWhere(events => (events & (int)GeoEvents.Shape) != 0, OnInputsChanged)
                    .OnDisposing(OnInputsChanged);
            }
            OnInputsChanged();
        }

        // Shape
        private void OnInputsChanged()
        {
            if (IsDisposed) return;
            try { RebuildShape(); }
            finally { OnUpdated((int)GeoEvents.Shape); }
        }

        private void RebuildShape()
        {
            _hinges.Clear();
            Length = 0f;
            if (IsDisposed || From == null || To == null || _handles.Any(handle => handle.IsDisposed)) return;

            var from = From.Position;
            var to = To.Position;
            var positions = new List<P>();
            var handles = _handles.Select(handle => handle.Position).ToArray();
            if (!_shaper.TryShape(from, to, _values, handles, positions) || positions.Count < 2) return;
            if (!EqualityComparer<P>.Default.Equals(positions[0], from)
                || !EqualityComparer<P>.Default.Equals(positions[positions.Count - 1], to)) return;

            var hinges = new List<Hinge<P, R>>();
            if (!TryBuildHinges(positions, hinges, out var length)
                || hinges.Count < 2 || !Location.IsFinite(length) || length <= 0f) return;
            _hinges.AddRange(hinges);
            Length = length;
        }

        protected abstract bool TryBuildHinges(
            IReadOnlyList<P> positions,
            List<Hinge<P, R>> hinges,
            out float length);

        // Lifetime
        protected override void OnDisposing()
        {
            foreach (var handle in _subscribedHandles) handle.EventHub.Disconnect(this);
            _subscribedHandles.Clear();
            _hinges.Clear();
            Length = 0f;
            base.OnDisposing();
        }
    }
}
