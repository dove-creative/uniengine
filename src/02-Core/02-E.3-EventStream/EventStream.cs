using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core.EventStreamCore;

namespace Blackthunder.UniEngine.Core
{
    public class EventStream<TEventArgs> : IEventStream<TEventArgs>, IPassiveObserver
    {
        // Front
        public bool Enabled
        {
            get => _isEnabled;
            set
            {
                if (value) ThrowIfDisposed();
                _isEnabled = value;
            }
        }

        public TerminationMode TerminationMode
        {
            get => _terminationMode;
            set
            {
                if (IsDisposing || _isTerminating) return;
                _terminationMode = value;
            }
        }

        public bool IsDisposed { get; private set; }


        // Internal
        private bool _isEnabled = true;
        private bool _isTerminating;
        private TerminationMode _terminationMode = TerminationMode.Complete;
        protected bool IsDisposing { get; private set; }

        private static readonly Action _emptyCompleted = () => { };
        private static readonly Action<Exception> _emptyErrored = _ => { };
        private static readonly Action<Action<TEventArgs>, TEventArgs> _invokeUpdated = static (action, args) => action(args);

        protected AdaptiveAction<TEventArgs> Updated { get; } = new(true);
        protected AdaptiveAction Completed { get; } = new(true);
        protected AdaptiveAction<Exception> Errored { get; } = new(true);

        private readonly List<IDisposable> _handles = new();
        private readonly HashSet<IDisposable> _connections = new();
        private readonly Dictionary<Delegate, Handle> _subscriptionHandles = new();
        private readonly Dictionary<Delegate, Delegate[]> _subscriptionCallbacks = new();


        // Content --------------------
        // Construction
        // Prepare shared delegates before the first instance can publish.
        static EventStream() { }

        // Propagation
        public virtual void OnNext(TEventArgs args)
        {
            ThrowIfDisposed();
            if (!Enabled) return;

            Dispatch(Updated, args, _invokeUpdated);
        }

        public void OnCompleted() =>
            Terminate(() => Dispatch(Completed, action => action()));

        public void OnError(Exception exception) =>
            Terminate(() => Dispatch(Errored, action => action(exception)));

        public IEventStream<TEventArgs> With(TerminationMode terminationMode)
        {
            TerminationMode = terminationMode;
            return this;
        }

        protected void Dispatch<TAction>(
            AdaptiveCollection<TAction> actions,
            Action<TAction> invoke,
            Func<TAction, bool> filter = null)
            where TAction : Delegate
        {
            Dispatch(
                actions, (Invoke: invoke, Filter: filter),
                static (action, state) => state.Invoke(action),
                static (action, state) => state.Filter == null || state.Filter(action));
        }

        protected void Dispatch<TAction, TState>(
            AdaptiveCollection<TAction> actions,
            TState state,
            Action<TAction, TState> invoke,
            Func<TAction, TState, bool> filter = null)
            where TAction : Delegate
        {
            foreach (var action in actions)
            {
                if (IsDisposing) break;
                if (action is null || (filter != null && !filter(action, state))) continue;

                _subscriptionHandles.TryGetValue(action, out var handle);
                var callbacks = _subscriptionCallbacks.TryGetValue(action, out var original)
                    ? original : InvocationListCache<TAction>.Get(action);
                if (callbacks == null) continue;

                foreach (var callback in callbacks)
                {
                    if (IsDisposing || !actions.IsCurrentItemActive || (handle?.IsDisposed ?? false))
                        break;

                    try
                    {
                        invoke((TAction)callback, state);
                    }
                    catch (Exception exception)
                    {
                        ExceptionDispatchInfo releaseFailure = null;
                        if (handle == null)
                        {
                            actions.RemoveCurrentItem();
                        }
                        else
                        {
                            try
                            {
                                handle.Dispose();
                            }
                            catch (Exception releaseException)
                            {
                                releaseFailure = ExceptionDispatchInfo.Capture(releaseException);
                            }
                        }

                        ExceptionHandler.Report(exception);
                        releaseFailure?.Throw();
                        break;
                    }
                }
            }
        }

        private void Terminate(Action notification)
        {
            if (IsDisposing || _isTerminating) return;
            _isTerminating = true;

            ExceptionDispatchInfo failure = null;
            try
            {
                notification();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                try
                {
                    Dispose();
                }
                catch (Exception exception)
                {
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                }
            }

            failure?.Throw();
        }

        // Subscriptions
        public virtual IDisposable Subscribe(Action<TEventArgs> updated)
        {
            var handle = CreateSubscription(updated, null, null, out var registeredUpdated);
            Updated.Add(registeredUpdated);
            return handle;
        }

        public virtual IDisposable Subscribe(Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            var handle = CreateSubscription(updated, completed, errored, out var registeredUpdated);
            Updated.Add(registeredUpdated);
            return handle;
        }

        public IDisposable Subscribe(IObserver<TEventArgs> observer)
        {
            ThrowIfDisposed();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            return Subscribe(observer.OnNext, observer.OnCompleted, observer.OnError);
        }

        protected IDisposable CreateSubscription(
            Action<TEventArgs> updated, Action completed, Action<Exception> errored,
            out Action<TEventArgs> registeredUpdated)
        {
            ThrowIfDisposed();

            var updatedCallbacks = updated == null ? null : InvocationListCache<Action<TEventArgs>>.Get(updated);
            var completedCallbacks = completed == null ? null : InvocationListCache<Action>.Get(completed);
            var erroredCallbacks = errored == null ? null : InvocationListCache<Action<Exception>>.Get(errored);

            registeredUpdated = args => updated?.Invoke(args);
            Action registeredCompleted = () => completed?.Invoke();
            Action<Exception> registeredErrored = exception => errored?.Invoke(exception);
            var handle = CreateSubscriptionHandle(registeredUpdated, registeredCompleted, registeredErrored);

            _subscriptionCallbacks[registeredUpdated] = updatedCallbacks;
            _subscriptionCallbacks[registeredCompleted] = completedCallbacks;
            _subscriptionCallbacks[registeredErrored] = erroredCallbacks;

            if (completed != null) Completed.Add(registeredCompleted);
            if (errored != null) Errored.Add(registeredErrored);
            return handle;
        }

        protected bool IsSubscriptionActive(Action<TEventArgs> updated) =>
            _subscriptionHandles.TryGetValue(updated, out var handle) && !handle.IsDisposed;

        protected IDisposable CreateSubscriptionHandle(Action<TEventArgs> updated) =>
            CreateSubscriptionHandle(updated, _emptyCompleted, _emptyErrored);

        protected IDisposable CreateSubscriptionHandle(Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            ThrowIfDisposed();

            Handle handle = null;
            handle = new Handle(() =>
            {
                if (IsDisposing) return;
                _handles.Remove(handle);
                RemoveSubscriptionMetadata(updated, handle);
                RemoveSubscriptionMetadata(completed, handle);
                RemoveSubscriptionMetadata(errored, handle);

                RemoveConnection(updated, completed, errored);

                if (TerminationMode == TerminationMode.NoSubscribers && _handles.Count == 0)
                    Dispose();
            });

            _handles.Add(handle);
            if (updated != null) _subscriptionHandles[updated] = handle;
            if (completed != null) _subscriptionHandles[completed] = handle;
            if (errored != null) _subscriptionHandles[errored] = handle;
            return handle;
        }

        private void RemoveSubscriptionMetadata(Delegate callback, Handle handle)
        {
            if (callback == null
                || !_subscriptionHandles.TryGetValue(callback, out var registeredHandle)
                || !ReferenceEquals(handle, registeredHandle))
                return;

            _subscriptionHandles.Remove(callback);
            _subscriptionCallbacks.Remove(callback);
        }

        protected virtual void RemoveConnection(Action<TEventArgs> updated, Action completed, Action<Exception> errored)
        {
            Updated.Remove(updated);
            Completed.Remove(completed);
            Errored.Remove(errored);
        }

        // Connections
        void IPassiveObserver.TakeConnection(IDisposable connection)
        {
            ThrowIfDisposed();
            if (connection != null)
                _connections.Add(connection);
        }

        public TObserver Chain<TObserver>(TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            ThrowIfDisposed();
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));
            if (ReferenceEquals(observer, this))
                throw new ArgumentException("A stream cannot chain itself.", nameof(observer));

            connection = Subscribe(observer);
            BindConnection(observer, connection);
            return observer;
        }

        protected internal static void BindConnection<TObserver>(TObserver observer, IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            if (observer is not IPassiveObserver passiveObserver) return;

            try
            {
                passiveObserver.TakeConnection(connection);
            }
            catch (Exception exception)
            {
                try
                {
                    connection.Dispose();
                }
                finally
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                throw;
            }
        }

        public IEventStream<TEventArgs> Connect<TObserver>(TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            Chain(observer, out connection);
            return this;
        }

        // Disposal
        internal static void ThrowIfDisposed(IEventStream<TEventArgs> stream)
        {
            if (stream is EventStream<TEventArgs> eventStream)
                eventStream.ThrowIfDisposed();
            else if (stream.IsDisposed)
                throw new ObjectDisposedException(stream.GetType().Name);
        }

        protected void ThrowIfDisposed()
        {
            if (IsDisposed || IsDisposing || _isTerminating)
                throw new ObjectDisposedException(GetType().Name);
        }

        public virtual void Dispose()
        {
            if (IsDisposing) return;
            IsDisposing = true;

            ExceptionDispatchInfo failure = null;
            try
            {
                Updated.Clear();
                Completed.Clear();
                Errored.Clear();

                var disposables = new List<IDisposable>(_connections);
                for (var i = _handles.Count - 1; i >= 0; i--)
                    disposables.Add(_handles[i]);

                foreach (var disposable in disposables)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception exception)
                    {
                        failure ??= ExceptionDispatchInfo.Capture(exception);
                    }
                }
            }
            finally
            {
                _connections.Clear();
                _handles.Clear();
                _subscriptionHandles.Clear();
                _subscriptionCallbacks.Clear();
                IsDisposed = true;
            }

            failure?.Throw();
        }
    }
}
