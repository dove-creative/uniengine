using System;
using System.Collections;
using System.Collections.Generic;

namespace Blackthunder.UniEngine.Core
{
    public class AdaptiveHashSet<T> : ICollection<T>, IEnumerationSource<T>
    {
        // Front
        public int Count => _isIterating
            ? _items.Count - _removingItems.Count + _pendingItems.Count
            : _items.Count;

        public bool IsReadOnly { get; } = false;
        public bool DeferredUpdate { get; private set; }

        // Internal
        private readonly HashSet<T> _items = new();
        private readonly HashSet<T> _pendingItems = new();
        private readonly HashSet<T> _removingItems = new();
        private readonly HashSet<T> _iterationItems = new();

        private HashSet<T>.Enumerator _enumerator;
        private long _enumerationId;
        private bool _isIterating;
        private bool _iterationStopped;


        // Content --------------------
        // Construction
        public AdaptiveHashSet()
            : this(true) { }
        public AdaptiveHashSet(bool deferredUpdate) => DeferredUpdate = deferredUpdate;

        // Iteration
        public ValueEnumerator<T> GetEnumerator() => new(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        long IEnumerationSource<T>.BeginEnumeration()
        {
            if (_isIterating)
                throw new InvalidOperationException("AdaptiveHashSet is already being iterated. Nested iterations are not allowed.");

            _enumerationId = checked(_enumerationId + 1);
            _enumerator = _items.GetEnumerator();
            _isIterating = true;
            _iterationStopped = false;
            return _enumerationId;
        }

        bool IEnumerationSource<T>.TryMoveNext(long enumerationId, out T current)
        {
            if (!_isIterating || enumerationId != _enumerationId)
                throw new InvalidOperationException("The enumeration is no longer active.");

            while (!_iterationStopped)
            {
                while (_enumerator.MoveNext())
                {
                    var item = _enumerator.Current;
                    // Skip the lookup when nothing is marked, so clean passes never hash items.
                    if (_removingItems.Count != 0 && _removingItems.Contains(item)) continue;

                    current = item;
                    return true;
                }

                _enumerator.Dispose();
                if (DeferredUpdate || _pendingItems.Count == 0) break;

                // Keep a stable batch while callbacks record the next pending changes.
                _iterationItems.Clear();
                foreach (var item in _pendingItems) _iterationItems.Add(item);
                ApplyChanges();
                _enumerator = _iterationItems.GetEnumerator();
            }

            current = default;
            return false;
        }

        void IEnumerationSource<T>.EndEnumeration(long enumerationId)
        {
            if (!_isIterating || enumerationId != _enumerationId) return;

            try
            {
                _enumerator.Dispose();
                ApplyChanges();
            }
            finally
            {
                _enumerator = default;
                _iterationItems.Clear();
                _removingItems.Clear();
                _pendingItems.Clear();
                _isIterating = false;
                _iterationStopped = false;
            }
        }

        private void ApplyChanges()
        {
            foreach (var item in _removingItems) _items.Remove(item);
            foreach (var item in _pendingItems) _items.Add(item);

            _removingItems.Clear();
            _pendingItems.Clear();
        }

        public void ForEach(Action<T> selector)
        {
            foreach (var item in this)
                selector(item);
        }

        // Modification
        public void Add(T item)
        {
            if (_isIterating)
            {
                if (!Contains(item))
                    _pendingItems.Add(item);
            }
            else
                _items.Add(item);
        }
        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
                Add(item);
        }

        public bool Remove(T item)
        {
            if (_isIterating)
            {
                if (_pendingItems.Contains(item))
                {
                    _pendingItems.Remove(item);
                    return true;
                }

                if (_removingItems.Contains(item))
                    return false;

                if (_items.Contains(item))
                {
                    _removingItems.Add(item);
                    return true;
                }

                return false;
            }

            return _items.Remove(item);
        }

        public void Clear()
        {
            _items.Clear();
            _removingItems.Clear();
            _pendingItems.Clear();
            _iterationItems.Clear();

            if (_isIterating) _iterationStopped = true;
        }

        // Inspection
        public bool Contains(T item)
        {
            if (_isIterating)
            {
                if (_pendingItems.Contains(item))
                    return true;

                return _items.Contains(item) && !_removingItems.Contains(item);
            }

            return _items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (_isIterating)
                throw new InvalidOperationException("AdaptiveHashSet cannot be copied during iteration.");

            _items.CopyTo(array, arrayIndex);
        }
    }
}
