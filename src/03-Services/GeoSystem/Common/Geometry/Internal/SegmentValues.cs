using System;
using System.Collections;
using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Geometry
{
    internal class SegmentValues : IDictionary<string, float>, IReadOnlyDictionary<string, float>
    {
        // Front
        public float this[string key]
        {
            get => _values[key];
            set
            {
                Validate(key, value);
                if (_values.TryGetValue(key, out var previous) && previous.Equals(value)) return;
                _values[key] = value;
                _changed();
            }
        }
        public int Count => _values.Count;
        public bool IsReadOnly => false;
        public ICollection<string> Keys => _values.Keys;
        public ICollection<float> Values => _values.Values;
        IEnumerable<string> IReadOnlyDictionary<string, float>.Keys => Keys;
        IEnumerable<float> IReadOnlyDictionary<string, float>.Values => Values;

        // Internal
        private readonly Dictionary<string, float> _values = new(StringComparer.Ordinal);
        private readonly Action _guard;
        private readonly Action _changed;


        // Content
        public SegmentValues(Action guard, Action changed)
        {
            _guard = guard;
            _changed = changed;
        }

        private void Validate(string key, float value)
        {
            _guard();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key == ShaperValues.TargetSegmentLength && (!Location.IsFinite(value) || value <= 0f))
                throw new ArgumentOutOfRangeException(
                    nameof(value), "Target segment length must be finite and positive.");
        }

        public void Add(string key, float value)
        {
            Validate(key, value);
            _values.Add(key, value);
            _changed();
        }
        public void Add(KeyValuePair<string, float> item) => Add(item.Key, item.Value);

        public bool Remove(string key)
        {
            _guard();
            if (!_values.Remove(key)) return false;
            _changed();
            return true;
        }
        public bool Remove(KeyValuePair<string, float> item)
        {
            _guard();
            return Contains(item) && Remove(item.Key);
        }

        public void Clear()
        {
            _guard();
            if (_values.Count == 0) return;
            _values.Clear();
            _changed();
        }

        public bool ContainsKey(string key) => _values.ContainsKey(key);
        public bool TryGetValue(string key, out float value) => _values.TryGetValue(key, out value);
        public bool Contains(KeyValuePair<string, float> item) =>
            ((ICollection<KeyValuePair<string, float>>)_values).Contains(item);
        public void CopyTo(KeyValuePair<string, float>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<string, float>>)_values).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<string, float>> GetEnumerator() => _values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
