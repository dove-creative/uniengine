using System;
using System.Collections;
using System.Collections.Generic;

namespace BlackThunder.UniEngine.Core
{
    public class AdaptiveCollection<T> : ICollection<T>, IEnumerationSource<T>
    {
        // Front
        public int Count => DeferredUpdate && _isIterating ? _items.Count + _pendingItems.Count : _items.Count;

        public bool IsReadOnly { get; } = false;
        public bool DeferredUpdate { get; private set; }

        // Internal
        private readonly List<T> _items = new();
        private readonly List<T> _pendingItems;

        private int _index = -1;
        private long _enumerationId;
        private bool _isIterating;
        private bool _iterationStopped;
        private bool _currentItemRemoved;

        internal bool IsCurrentItemActive => _isIterating && !_iterationStopped && !_currentItemRemoved;


        // Content --------------------
        // Construction
        public AdaptiveCollection() : this(true) { }
        public AdaptiveCollection(bool deferredUpdate)
        {
            DeferredUpdate = deferredUpdate;

            if (DeferredUpdate) _pendingItems = new();
        }

        // Iteration
        public ValueEnumerator<T> GetEnumerator() => new(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        long IEnumerationSource<T>.BeginEnumeration()
        {
            if (_isIterating)
                throw new InvalidOperationException(
                    "Collection is already being iterated. Nested iterations are not allowed.");

            _enumerationId = checked(_enumerationId + 1);
            _isIterating = true;
            _iterationStopped = false;
            _currentItemRemoved = false;
            _index = -1;
            return _enumerationId;
        }

        bool IEnumerationSource<T>.TryMoveNext(long enumerationId, out T current)
        {
            if (!_isIterating || enumerationId != _enumerationId)
                throw new InvalidOperationException("The enumeration is no longer active.");

            if (!_iterationStopped && ++_index < _items.Count)
            {
                _currentItemRemoved = false;
                current = _items[_index];
                return true;
            }

            current = default;
            return false;
        }

        void IEnumerationSource<T>.EndEnumeration(long enumerationId)
        {
            if (!_isIterating || enumerationId != _enumerationId)
                return;

            try
            {
                if (DeferredUpdate)
                    _items.AddRange(_pendingItems);
            }
            finally
            {
                _pendingItems?.Clear();
                _isIterating = false;
                _iterationStopped = false;
                _currentItemRemoved = false;
                _index = -1;
            }
        }

        public void ForEach(Action<T> selector)
        {
            foreach (var item in this)
                selector(item);
        }

        // Modification
        public void Add(T item)
        {
            if (DeferredUpdate && _isIterating)
                _pendingItems.Add(item);
            else
                _items.Add(item);

            OnItemAdded(item);
        }
        public void AddRange(IEnumerable<T> items)
        {
            var destination = DeferredUpdate && _isIterating ? _pendingItems : _items;
            if (items != null && items is not ICollection<T>)
            {
                // Prepare each streamed item before the source can run more user code.
                foreach (var item in items)
                {
                    destination.Add(item);
                    OnItemAdded(item);
                }

                return;
            }

            var previousCount = destination.Count;
            try
            {
                destination.AddRange(items);
            }
            finally
            {
                // An enumerable can fail after appending some of its items.
                var count = destination.Count;
                for (var index = previousCount; index < count; index++)
                    OnItemAdded(destination[index]);
            }
        }

        protected virtual void OnItemAdded(T item) { }

        public bool Remove(T item)
        {
            var index = _items.IndexOf(item);
            if (index >= 0)
            {
                if (_isIterating && index <= _index)
                {
                    if (index == _index) _currentItemRemoved = true;

                    _index--;
                }

                _items.RemoveAt(index);
                return true;
            }

            return DeferredUpdate && _isIterating && _pendingItems.Remove(item);
        }

        internal void RemoveCurrentItem()
        {
            if (!IsCurrentItemActive) return;

            _items.RemoveAt(_index);
            _index--;
            _currentItemRemoved = true;
        }

        public void Clear()
        {
            _items.Clear();
            _pendingItems?.Clear();

            if (_isIterating) _iterationStopped = true;
        }

        // Inspection
        public bool Contains(T item) => DeferredUpdate && _isIterating
            ? _items.Contains(item) || _pendingItems.Contains(item)
            : _items.Contains(item);

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);

            if (DeferredUpdate && _isIterating)
                _pendingItems.CopyTo(array, arrayIndex + _items.Count);
        }
    }
}
