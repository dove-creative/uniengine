using System;
using System.Collections;
using System.Collections.Generic;

namespace Blackthunder.UniEngine.Core
{
    public class AdaptiveDictionary<TKey, TValue> : ICollection<KeyValuePair<TKey, TValue>>, IDictionary<TKey, TValue>, IDictionary
    {
        // Front
        public int Count => _isIterating
            ? _items.Count - _removingKeys.Count + _pendingItems.Count
            : _items.Count;

        public bool IsReadOnly { get; } = false;
        public bool IsFixedSize { get; } = false;
        public bool DeferredUpdate { get; private set; }

        public ICollection<TKey> Keys => !_isIterating ? _items.Keys : throw new NotSupportedException("The Keys property cannot be accessed during iteration.");
        public ICollection<TValue> Values => !_isIterating ? _items.Values : throw new NotSupportedException("The Values property cannot be accessed during iteration.");

        public bool IsSynchronized { get; } = false;
        public object SyncRoot { get; } = new();

        /// <summary>
        /// Gets or sets the value for the specified key
        /// </summary>
        /// <remarks>
        /// - If modified during a deferred iteration, the value will not be included in the current iteration but will be available in the next iteration.<br/>
        /// - If modified during an immediate iteration, the value will be traversed at the end of the current iteration, regardless of whether it has already been iterated over.
        /// </remarks>
        public TValue this[TKey key]
        {
            get
            {
                if (!TryGetValue(key, out var value))
                    throw new KeyNotFoundException($"The given key '{key}' was not present in the AdaptiveDictionary.");

                return value;
            }
            set
            {
                if (_isIterating)
                {
                    Remove(key);
                    Add(key, value);
                }
                else
                    _items[key] = value;
            }
        }

        // Internal
        private readonly Dictionary<TKey, TValue> _items = new();

        private readonly Dictionary<TKey, TValue> _pendingItems = new();
        private readonly HashSet<TKey> _removingKeys = new();

        private bool _isIterating;
        private bool _iterationStopped;


        // Content
        public AdaptiveDictionary()
            : this(true) { }
        public AdaptiveDictionary(bool deferredUpdate)
        {
            DeferredUpdate = deferredUpdate;
        }

        #region Iteration
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            if (_isIterating) throw new InvalidOperationException("AdaptiveDictionary is already being iterated. Nested iterations are not allowed.");
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
                        if (_removingKeys.Contains(item.Key))
                            continue;

                        yield return item;
                    }

                    if (_iterationStopped || DeferredUpdate || _pendingItems.Count == 0)
                        break;

                    items = new Dictionary<TKey, TValue>(_pendingItems);
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
                    _removingKeys.Clear();
                    _pendingItems.Clear();
                    _isIterating = false;
                    _iterationStopped = false;
                }
            }


            void ApplyChanges()
            {
                foreach (var key in _removingKeys)
                    _items.Remove(key);

                foreach (var item in _pendingItems)
                    _items[item.Key] = item.Value;

                _removingKeys.Clear();
                _pendingItems.Clear();
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        public void ForEach(Action<KeyValuePair<TKey, TValue>> selector)
        {
            foreach (var item in this)
                selector(item);
        }
        public void ForEach(Action<TKey, TValue> selector)
        {
            foreach (var (key, value) in this)
                selector(key, value);
        }
        public void ForEach(Action<TValue> selector)
        {
            foreach (var (_, value) in this)
                selector(value);
        }
        #endregion


        public bool ContainsKey(TKey key) => TryGetValue(key, out _);
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_isIterating)
            {
                if (_pendingItems.TryGetValue(key, out value))
                    return true;

                if (_items.TryGetValue(key, out value))
                    return !_removingKeys.Contains(key);

                return false;
            }
            else
                return _items.TryGetValue(key, out value);
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
        public void Add(TKey key, TValue value)
        {
            if (_isIterating)
            {
                if (ContainsKey(key))
                    throw new ArgumentException($"An item with the same key '{key}' already exists.");

                _pendingItems[key] = value;
            }
            else
                _items.Add(key, value);
        }

        public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            foreach (var item in items)
                Add(item);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);
        public bool Remove(TKey key)
        {
            if (_isIterating)
            {
                if (_pendingItems.ContainsKey(key))
                {
                    _pendingItems.Remove(key);
                    return true;
                }

                if (_removingKeys.Contains(key))
                    return false;

                if (_items.ContainsKey(key))
                {
                    _removingKeys.Add(key);
                    return true;
                }

                return false;
            }
            else
                return _items.Remove(key);
        }

        public void Clear()
        {
            _items.Clear();
            _removingKeys.Clear();
            _pendingItems.Clear();

            if (_isIterating) _iterationStopped = true;
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (!TryGetValue(item.Key, out var value)) return false;
            return item.Value.Equals(value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (_isIterating)
                throw new InvalidOperationException("AdaptiveDictionary cannot be copied during iteration.");
            if (array == null)
                throw new ArgumentNullException(nameof(array), "Target array cannot be null.");
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), "Array index must be non-negative.");
            if (array.Length - arrayIndex < _items.Count)
                throw new ArgumentException("The target array is not large enough to hold the collection items.");

            foreach (var kvp in _items)
                array[arrayIndex++] = kvp;
        }


        #region IDictionary
        ICollection IDictionary.Keys => !_isIterating ? _items.Keys : throw new NotSupportedException("The Count property cannot be accessed during iteration.");
        ICollection IDictionary.Values => !_isIterating ? _items.Values : throw new NotSupportedException("The Value property cannot be accessed during iteration.");

        object IDictionary.this[object key]
        {
            get
            {
                if (key is not TKey tKey)
                    throw new ArgumentException("Key is of incorrect type.", nameof(key));

                return this[tKey];
            }
            set
            {
                if (key is not TKey tKey)
                    throw new ArgumentException("Key is of incorrect type.", nameof(key));
                if (value is not TValue tValue)
                    throw new ArgumentException("Value is of incorrect type.", nameof(value));

                this[tKey] = tValue;
            }
        }

        IDictionaryEnumerator IDictionary.GetEnumerator() => new DictionaryEnumerator<TKey, TValue>(this);
        private class DictionaryEnumerator<UKey, UValue> : IDictionaryEnumerator, IDisposable
        {
            private IEnumerator<KeyValuePair<UKey, UValue>> _currentEnumerator;
            private readonly AdaptiveDictionary<UKey, UValue> _dictionary;

            public DictionaryEnumerator(AdaptiveDictionary<UKey, UValue> dictionary)
            {
                _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
                _currentEnumerator = _dictionary.GetEnumerator();
            }

            public object Key => _currentEnumerator.Current.Key;
            public object Value => _currentEnumerator.Current.Value;

            public DictionaryEntry Entry => new(Key, Value);

            public bool MoveNext() => _currentEnumerator.MoveNext();
            public void Reset() => throw new NotSupportedException("AdaptiveDictionary cannot be reset during iteration.");

            public object Current => Entry;

            public void Dispose()
            {
                _currentEnumerator?.Dispose();
                _currentEnumerator = null;
            }
        }


        void IDictionary.Add(object key, object value) => Add((TKey)key, (TValue)value);
        void IDictionary.Remove(object key) => Remove((TKey)key);
        bool IDictionary.Contains(object key) => ContainsKey((TKey)key);

        void ICollection.CopyTo(Array array, int index)
        {
            if (_isIterating)
                throw new NotSupportedException("AdaptiveDictionary cannot be copied during iteration.");
            if (array == null)
                throw new ArgumentNullException(nameof(array), "Target array cannot be null.");
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "Array index must be non-negative.");
            if (array.Rank != 1)
                throw new ArgumentException("Target array must be single-dimensional.", nameof(array));
            if (array.Length - index < _items.Count)
                throw new ArgumentException("The target array is not large enough to hold the collection items.");

            if (array is KeyValuePair<TKey, TValue>[] pairs)
            {
                CopyTo(pairs, index);
            }
            else
            {
                foreach (var item in _items)
                {
                    array.SetValue(item, index++);
                }
            }
        }
        #endregion
    }
}
