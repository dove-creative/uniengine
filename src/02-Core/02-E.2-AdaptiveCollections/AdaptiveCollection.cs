using System;
using System.Collections;
using System.Collections.Generic;

namespace Blackthunder.UniEngine.Core
{
    public class AdaptiveCollection<T> : ICollection<T>
    {
        // Front
        public int Count => DeferredUpdate && _isIterating
            ? _items.Count + _pendingItems.Count
            : _items.Count;

        public bool IsReadOnly { get; } = false;
        public bool DeferredUpdate { get; private set; }

        // Internal
        private readonly List<T> _items = new();
        private readonly List<T> _pendingItems;

        private int _index = -1;
        private bool _isIterating;
        private bool _iterationStopped;
        private bool _currentItemRemoved;

        internal bool IsCurrentItemActive => _isIterating && !_iterationStopped && !_currentItemRemoved;


        // Content --------------------
        // Construction
        public AdaptiveCollection()
            : this(true) { }
        public AdaptiveCollection(bool deferredUpdate)
        {
            DeferredUpdate = deferredUpdate;
            if (DeferredUpdate) _pendingItems = new();
        }

        // Iteration
        public IEnumerator<T> GetEnumerator()
        {
            if (_isIterating)
                throw new InvalidOperationException("Collection is already being iterated. Nested iterations are not allowed.");

            _isIterating = true;
            _iterationStopped = false;
            _index = -1;

            try
            {
                while (!_iterationStopped && ++_index < _items.Count)
                {
                    _currentItemRemoved = false;
                    yield return _items[_index];
                }
            }
            finally
            {
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
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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
        }
        public void AddRange(IEnumerable<T> items)
        {
            if (DeferredUpdate && _isIterating)
                _pendingItems.AddRange(items);
            else
                _items.AddRange(items);
        }

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
