using System.Collections.Generic;

namespace BlackThunder.GeoSystem.Navigation
{
    internal class MinHeap<T>
    {
        // Front
        public int Count => _items.Count;

        // Internal
        private readonly List<Item> _items = new();
        private long _nextOrder;

        private readonly struct Item
        {
            public T Value { get; }
            public double Priority { get; }
            public long Order { get; }


            public Item(T value, double priority, long order)
            {
                Value = value;
                Priority = priority;
                Order = order;
            }

            public bool IsBefore(Item other) =>
                Priority < other.Priority || (Priority == other.Priority && Order < other.Order);
        }


        // Content
        public void Enqueue(T value, double priority)
        {
            var item = new Item(value, priority, _nextOrder++);
            var index = _items.Count;
            _items.Add(item);
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (!item.IsBefore(_items[parent])) break;
                _items[index] = _items[parent];
                index = parent;
            }
            _items[index] = item;
        }

        public T Dequeue()
        {
            var value = _items[0].Value;
            var last = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            if (_items.Count == 0) return value;

            var index = 0;
            while (index * 2 + 1 < _items.Count)
            {
                var child = index * 2 + 1;
                if (child + 1 < _items.Count && _items[child + 1].IsBefore(_items[child])) child++;
                if (!_items[child].IsBefore(last)) break;
                _items[index] = _items[child];
                index = child;
            }
            _items[index] = last;
            return value;
        }
    }
}
