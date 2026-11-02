using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Blackthunder.UniEngine.Core
{
    public class Property : IDictionary<object, object>
    {
        // Front
        public int Count => _properties.Count;
        public bool HasValue => Count > 0;

        public object this[object key]
        {
            get => Get(key);
            set => _properties[key] = value;
        }

        // Property
        ICollection<object> IDictionary<object, object>.Keys => _properties.Keys;
        ICollection<object> IDictionary<object, object>.Values => _properties.Values;
        bool ICollection<KeyValuePair<object, object>>.IsReadOnly { get; } = false;

        // Internal
        private readonly Dictionary<object, object> _properties = new();


        // Content
        public Property() { }
        public Property(object key, object value) => Add(key, value);

        public T Add<T>(T key) => Add(key, key);
        public T Add<T>(object key, T property)
        {
            if (_properties.ContainsKey(key))
                throw new ArgumentException(Ctx($"The same key already exists. Key: {key}"));

            _properties[key] = property;
            return property;
        }
        void IDictionary<object, object>.Add(object key, object property) => Add(key, property);
        void ICollection<KeyValuePair<object, object>>.Add(KeyValuePair<object, object> item) => Add(item.Key, item.Value);

        public T Get<T>(object key) => TryGet(key, out T value)
            ? value : throw new KeyNotFoundException(Ctx($"No properties were found for the key you entered. Key: {key}"));
        public object Get(object key) => TryGet(key, out var value)
            ? value : throw new KeyNotFoundException(Ctx($"No properties were found for the key you entered. Key: {key}"));

        public bool TryGet<T>(object key, out T value)
        {
            if (!_properties.TryGetValue(key, out var targetValue))
            {
                value = default;
                return false;
            }

            if (targetValue == null && default(T) is null)
            {
                value = default;
                return true;
            }

            if (targetValue is not T)
                throw new InvalidCastException(Ctx(
                    $"The stored value '{targetValue?.ToString() ?? "null"}' has type '{targetValue?.GetType().FullName ?? "null"}', "
                    + $"which does not match the expected type '{typeof(T).FullName}'."));

            value = (T)targetValue;
            return true;
        }
        public bool TryGet(object key, out object value) => _properties.TryGetValue(key, out value);
        bool IDictionary<object, object>.TryGetValue(object key, out object value) => TryGet(key, out value);

        public bool Remove(object key) => _properties.Remove(key);
        bool IDictionary<object, object>.Remove(object key) => Remove(key);
        bool ICollection<KeyValuePair<object, object>>.Remove(KeyValuePair<object, object> item) =>
            ((ICollection<KeyValuePair<object, object>>)_properties).Remove(item);

        public T GetAndRemove<T>(object key)
        {
            if (!TryGet(key, out T value))
                throw new KeyNotFoundException(Ctx($"No properties were found for the key you entered. Key: {key}"));

            _properties.Remove(key);
            return value;
        }
        public object GetAndRemove(object key)
        {
            if (!TryGet(key, out var value))
                throw new KeyNotFoundException(Ctx($"No properties were found for the key you entered. Key: {key}"));

            _properties.Remove(key);
            return value;
        }

        public void Clear() => _properties.Clear();

        public bool ContainsKey(object key) => _properties.ContainsKey(key);
        bool ICollection<KeyValuePair<object, object>>.Contains(KeyValuePair<object, object> item) =>
            _properties.TryGetValue(item.Key, out var value) && Equals(value, item.Value);

        public static implicit operator bool(Property p) => p?.HasValue ?? false;
        public override string ToString()
        {
            if (Count == 0) return "Empty Property";

            var sb = new StringBuilder();
            var i = 0;
            sb.AppendLine($"Property [{_properties.Count}]");

            foreach (var property in _properties)
                sb.AppendLine($"({i++}) {property.Key.ToString()}: {property.Value?.ToString() ?? "null"}");

            return sb.ToString();
        }

        void ICollection<KeyValuePair<object, object>>.CopyTo(KeyValuePair<object, object>[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array), Ctx("The target array cannot be null."));
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), Ctx("The array index cannot be negative."));
            if (array.Length - arrayIndex < Count)
                throw new ArgumentException(Ctx("The target array is not large enough to hold the items."));

            var i = arrayIndex;
            foreach (var kvp in _properties)
            {
                array[i++] = kvp;
            }
        }

        IEnumerator<KeyValuePair<object, object>> IEnumerable<KeyValuePair<object, object>>.GetEnumerator() => _properties.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _properties.GetEnumerator();

        private string Ctx(string message) => $"[{nameof(Property)}] {message}";
    }
}
