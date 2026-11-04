using System;
using System.Collections.Generic;

namespace Blackthunder.UniEngine.Core
{
    public sealed class EventStream<T, TOrder> : EventStream<T>
        where TOrder : Enum
    {
        // Front
        public TOrder DefaultOrder { get; }
        public IReadOnlyList<TOrder> Orders { get; }

        // Internal
        private static readonly TOrder[] _enumOrders = (TOrder[])Enum.GetValues(typeof(TOrder));
        private readonly List<TOrder> _orders = new();
        private readonly Dictionary<TOrder, EventPath<T>> _paths = new();
        internal override EventPath<T> DefaultPath => _paths[DefaultOrder];


        // Content --------------------
        // Construction
        public EventStream()
            : this(_enumOrders) { }
        public EventStream(TOrder defaultOrder)
            : this(defaultOrder, _enumOrders) { }
        public EventStream(params TOrder[] orders)
            : this(First(orders), orders) { }
        public EventStream(TOrder defaultOrder, params TOrder[] orders)
        {
            DefaultOrder = defaultOrder;
            Orders = _orders.AsReadOnly();
            foreach (var order in orders == null || orders.Length == 0 ? _enumOrders : orders)
                AddOrder(order);
            AddOrder(defaultOrder);
        }

        private static TOrder First(TOrder[] orders)
        {
            if (orders == null || orders.Length == 0) orders = _enumOrders;
            return orders.Length == 0 ? default : orders[0];
        }

        // Groups
        public EventStream<T, TOrder> AddOrder(TOrder order)
        {
            ForOrder(order);
            return this;
        }

        public EventPath<T> ForOrder(TOrder order)
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            if (_paths.TryGetValue(order, out var path)) return path;

            path = CreateGroupPath(_orders.Count);
            _paths.Add(order, path);
            _orders.Add(order);
            return path;
        }

        public IDisposable Subscribe(TOrder order, Action<T> updated) =>
            ForOrder(order).Subscribe(updated);
        public IDisposable Subscribe(TOrder order, Action<T> updated, Action completed, Action<Exception> errored) =>
            ForOrder(order).Subscribe(updated, completed, errored);
        public IDisposable Subscribe(TOrder order, IObserver<T> observer) =>
            ForOrder(order).Subscribe(observer);
    }
}
