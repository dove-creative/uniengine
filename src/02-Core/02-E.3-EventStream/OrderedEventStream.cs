using System;
using System.Collections.Generic;
using System.Linq;

namespace Blackthunder.UniEngine.Core
{
    public class OrderedEventStream<TEventArgs, TOrder> : EventStream<TEventArgs>
        where TOrder : Enum
    {
        // Front
        public TOrder DefaultOrder { get; }
        public IReadOnlyList<TOrder> Orders => _orders;


        // Internal
        private static readonly Action<Action<TEventArgs>, TEventArgs> _invokeUpdated = static (action, args) => action(args);
        private readonly List<TOrder> _orders = new();
        private readonly Dictionary<TOrder, AdaptiveAction<TEventArgs>> _updatesByOrder = new();
        private readonly List<(AdaptiveAction<TEventArgs> Updates, Action<TEventArgs> Action)> _pendingUpdates = new();
        private bool _isPublishing;


        // Content --------------------
        // Construction
        // Prepare the invocation delegate before the first instance can publish.
        static OrderedEventStream() { }

        public OrderedEventStream()
            : this(GetDefaultOrder(), GetEnumOrders()) { }

        public OrderedEventStream(TOrder defaultOrder)
            : this(defaultOrder, GetEnumOrders()) { }

        public OrderedEventStream(params TOrder[] orders)
            : this(GetDefaultOrder(orders), orders) { }

        public OrderedEventStream(TOrder defaultOrder, params TOrder[] orders)
        {
            DefaultOrder = defaultOrder;

            var resolvedOrders = orders == null || orders.Length == 0
                ? GetEnumOrders() : orders;

            foreach (var order in resolvedOrders)
                AddOrder(order);

            AddOrder(DefaultOrder);
        }

        public OrderedEventStream<TEventArgs, TOrder> AddOrder(TOrder order)
        {
            ThrowIfDisposed();

            if (_updatesByOrder.ContainsKey(order))
                return this;

            _orders.Add(order);
            _updatesByOrder.Add(order, new AdaptiveAction<TEventArgs>(true));

            return this;
        }

        public override void OnNext(TEventArgs args)
        {
            ThrowIfDisposed();
            if (!Enabled) return;
            if (_isPublishing)
                throw new InvalidOperationException("The stream is already publishing. Nested propagation is not allowed.");

            _isPublishing = true;
            try
            {
                for (int i = 0, count = _orders.Count; i < count && !IsDisposing; i++)
                    Dispatch(_updatesByOrder[_orders[i]], args, _invokeUpdated);
            }
            finally
            {
                _isPublishing = false;
                if (!IsDisposing)
                {
                    foreach (var (updates, action) in _pendingUpdates)
                    {
                        if (IsSubscriptionActive(action))
                            updates.Add(action);
                    }
                }

                _pendingUpdates.Clear();
            }
        }


        // Subscriptions
        public override IDisposable Subscribe(Action<TEventArgs> updated) =>
            Subscribe(DefaultOrder, updated);

        public override IDisposable Subscribe(Action<TEventArgs> updated, Action completed, Action<Exception> errored) =>
            Subscribe(DefaultOrder, updated, completed, errored);

        public IDisposable Subscribe(Action<TEventArgs> updated, TOrder order) =>
            Subscribe(order, updated);

        public IDisposable Subscribe(Action<TEventArgs> updated, Action completed, Action<Exception> errored, TOrder order) =>
            Subscribe(order, updated, completed, errored);

        public IDisposable Subscribe(TOrder order, Action<TEventArgs> updated) =>
            SubscribeOrdered(order, updated, null, null);

        public IDisposable Subscribe(TOrder order, Action<TEventArgs> updated, Action completed, Action<Exception> errored) =>
            SubscribeOrdered(order, updated, completed, errored);

        public IDisposable Subscribe(TOrder order, IObserver<TEventArgs> observer)
        {
            ThrowIfDisposed();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            return Subscribe(order, observer.OnNext, observer.OnCompleted, observer.OnError);
        }

        private IDisposable SubscribeOrdered(
            TOrder order, Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            ThrowIfDisposed();
            var updates = GetUpdates(order);
            var handle = CreateSubscription(updated, completed, errored, out var registeredUpdated);

            if (_isPublishing)
                _pendingUpdates.Add((updates, registeredUpdated));
            else
                updates.Add(registeredUpdated);

            return handle;
        }

        // Connections
        public TObserver Chain<TObserver>(TOrder order)
            where TObserver : IObserver<TEventArgs>, new()
        {
            ThrowIfDisposed();
            return Chain(order, new TObserver(), out _);
        }

        public TObserver Chain<TObserver>(TOrder order, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>, new()
        {
            ThrowIfDisposed();
            return Chain(order, new TObserver(), out connection);
        }

        public TObserver Chain<TObserver>(TOrder order, TObserver observer)
            where TObserver : IObserver<TEventArgs> =>
            Chain(order, observer, out _);

        public TObserver Chain<TObserver>(TObserver observer, TOrder order)
            where TObserver : IObserver<TEventArgs> =>
            Chain(order, observer, out _);

        public TObserver Chain<TObserver>(TOrder order, TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            ThrowIfDisposed();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));
            if (ReferenceEquals(observer, this))
                throw new ArgumentException("A stream cannot chain itself.", nameof(observer));

            connection = Subscribe(order, observer);

            BindConnection(observer, connection);
            return observer;
        }

        public IEventStream<TEventArgs> Connect<TObserver>(TOrder order)
            where TObserver : IObserver<TEventArgs>, new()
        {
            ThrowIfDisposed();
            return Connect(order, new TObserver(), out _);
        }

        public IEventStream<TEventArgs> Connect<TObserver>(TOrder order, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>, new()
        {
            ThrowIfDisposed();
            return Connect(order, new TObserver(), out connection);
        }

        public IEventStream<TEventArgs> Connect<TObserver>(TOrder order, TObserver observer)
            where TObserver : IObserver<TEventArgs> =>
            Connect(order, observer, out _);

        public IEventStream<TEventArgs> Connect<TObserver>(TObserver observer, TOrder order)
            where TObserver : IObserver<TEventArgs> =>
            Connect(order, observer, out _);

        public IEventStream<TEventArgs> Connect<TObserver>(TOrder order, TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            Chain(order, observer, out connection);
            return this;
        }

        public EventStream<TEventArgs> Append(TOrder order)
        {
            ThrowIfDisposed();
            return Chain(order, new EventStream<TEventArgs>());
        }

        protected override void RemoveConnection(Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            foreach (var updates in _updatesByOrder.Values)
            {
                if (updates.Remove(updated))
                    break;
            }

            _pendingUpdates.RemoveAll(item => item.Action == updated);
            base.RemoveConnection(updated, completed, errored);
        }

        public override void Dispose()
        {
            try
            {
                base.Dispose();
            }
            finally
            {
                foreach (var updates in _updatesByOrder.Values)
                    updates.Clear();

                _pendingUpdates.Clear();
            }
        }

        private AdaptiveAction<TEventArgs> GetUpdates(TOrder order)
        {
            if (!_updatesByOrder.ContainsKey(order))
                AddOrder(order);

            return _updatesByOrder[order];
        }

        private static TOrder GetDefaultOrder() =>
            GetEnumOrders().FirstOrDefault();

        private static TOrder GetDefaultOrder(TOrder[] orders)
        {
            if (orders != null && orders.Length > 0)
                return orders[0];

            return GetEnumOrders().FirstOrDefault();
        }

        private static TOrder[] GetEnumOrders() =>
            Enum.GetValues(typeof(TOrder)).Cast<TOrder>().ToArray();
    }
}
