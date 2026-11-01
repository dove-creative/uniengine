using System;
using System.Collections;
using System.Collections.Generic;

namespace Blackthunder.UniEngine.Core
{
    public class AdaptiveHashSet<T> : ICollection<T>
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

        private bool _isIterating;
        private bool _iterationStopped;


        // Content --------------------
        // Construction
        public AdaptiveHashSet()
            : this(true) { }
        public AdaptiveHashSet(bool deferredUpdate) => DeferredUpdate = deferredUpdate;

        // Iteration
        public IEnumerator<T> GetEnumerator()
        {
            if (_isIterating)
                throw new InvalidOperationException("AdaptiveHashSet is already being iterated. Nested iterations are not allowed.");

            _isIterating = true;
            _iterationStopped = false;

            try
            {
                var items = _items;

                while (!_iterationStopped)
                {
                    using var enumerator = items.GetEnumerator();

                    while (!_iterationStopped && enumerator.MoveNext())
                    {
                        var item = enumerator.Current;
                        if (_removingItems.Contains(item))
                            continue;

                        yield return item;
                    }

                    if (_iterationStopped || DeferredUpdate || _pendingItems.Count == 0)
                        break;

                    items = new HashSet<T>(_pendingItems);
                    ApplyChanges();
                }
            }
            finally
            {
                try
                {
                    ApplyChanges();
                }
                finally
                {
                    _removingItems.Clear();
                    _pendingItems.Clear();
                    _isIterating = false;
                    _iterationStopped = false;
                }
            }

            void ApplyChanges()
            {
                _items.ExceptWith(_removingItems);
                _items.UnionWith(_pendingItems);

                _removingItems.Clear();
                _pendingItems.Clear();
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
