using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public partial class EventHub<TEventArgs>
    {
        /// <summary>
        /// Owns the registrations of one connection and disposes itself once it is empty.
        /// </summary>
        internal class Connection : IEventHubConnection<TEventArgs>, IDisposable
        {
            // Internal
            private readonly EventHub<TEventArgs> _hub;
            private readonly LinkedList<Subscription> _subscriptions = new();
            private object _key;
            private bool _isDisposed;
            private AdaptiveCollection<Delegate> _onDisposing;
            private AdaptiveCollection<Delegate> _onDisposed;

            private bool IsEmpty =>
                _subscriptions.Count == 0
                && !(_onDisposing?.Count > 0)
                && !(_onDisposed?.Count > 0);

            private class Subscription
            {
                public Delegate Source;
                public EventHubUpdateKind Kind;
                public PathInfo<TEventArgs> Path;
                public Delegate PredicateSource;
                public Handle Handle;
            }


            // Content
            public Connection(EventHub<TEventArgs> hub, object key)
            {
                _hub = hub;
                _key = key;
            }

            public void ThrowIfUnavailable()
            {
                _hub.ThrowIfDisposed();

                if (_isDisposed)
                    throw new InvalidOperationException(
                        _hub.Ctx("The listener registration has been disconnected."));
            }

            // Updates
            public void AddUpdate(
                Delegate source,
                EventHubUpdateKind kind,
                PathInfo<TEventArgs> path,
                Delegate predicateSource = null)
            {
                if (source == null)
                    return;

                foreach (var existing in _subscriptions)
                {
                    if (existing.Kind != kind || !Equals(existing.Source, source))
                        continue;

                    var samePath = existing.Path == null || path == null
                        ? existing.Path == path
                        : existing.Path.NodeType == path.NodeType && (path.NodeType switch
                        {
                            PathNodeType.Where => Equals(
                                existing.PredicateSource ?? existing.Path.Predicate, predicateSource ?? path.Predicate),
                            PathNodeType.When => EqualityComparer<TEventArgs>.Default.Equals(
                                existing.Path.Target, path.Target),
                            PathNodeType.Flags => existing.Path.Mask == path.Mask,
                            _ => false
                        });

                    if (samePath)
                    {
                        throw new InvalidOperationException(
                            _hub.Ctx("This update callback is already registered with this condition."));
                    }
                }

                // One stream subscription keeps adapted multicast members in the same propagation unit.
                Action<TEventArgs> callback = null;

                if (kind == EventHubUpdateKind.Typed)
                {
                    callback = (Action<TEventArgs>)source;
                }
                else if (kind == EventHubUpdateKind.Object)
                {
                    foreach (Action<object> entry in source.GetInvocationList())
                        callback += args => entry(args);
                }
                else
                {
                    foreach (Action entry in source.GetInvocationList())
                        callback += _ => entry();
                }

                var handle = ((IRegistrable<TEventArgs>)_hub._updates).Register(
                    Registration<TEventArgs>.Subscription(callback, null, null), path);

                var subscription = new Subscription
                {
                    Source = source,
                    Kind = kind,
                    Path = path,
                    PredicateSource = predicateSource,
                    Handle = (Handle)handle,
                };

                var node = _subscriptions.AddLast(subscription);

                // The stream disposes the handle when a callback or a condition above it fails.
                if (subscription.Handle.IsDisposed)
                    RemoveSubscription();
                else
                    subscription.Handle.Disposed += RemoveSubscription;

                void RemoveSubscription()
                {
                    // Disposing the connection or the hub owns its active traversal.
                    if (_isDisposed || _hub._pendingDispose || _hub._isDisposing || _hub.IsDisposed)
                        return;
                    if (node.List != _subscriptions)
                        return;

                    _subscriptions.Remove(node);
                    node.Value = null;
                    RemoveIfEmpty();
                }
            }

            // A direct -= removes only a root registration of the same kind.
            public void RemoveRootUpdate(Delegate source, EventHubUpdateKind kind)
            {
                for (var node = _subscriptions.First; node != null; node = node.Next)
                {
                    var subscription = node.Value;

                    if (subscription.Kind != kind
                        || subscription.Path != null
                        || !Equals(subscription.Source, source))
                        continue;

                    // The release notification removes the entry and, if it was the last one, the connection.
                    subscription.Handle.Dispose();
                    return;
                }
            }

            // Lifecycle
            public void AddDisposing(Delegate callback) => AddLifecycleCallback(ref _onDisposing, callback);
            public void AddDisposed(Delegate callback) => AddLifecycleCallback(ref _onDisposed, callback);

            private static void AddLifecycleCallback(ref AdaptiveCollection<Delegate> callbacks, Delegate callback)
            {
                if (callback == null)
                    return;

                callbacks ??= new AdaptiveCollection<Delegate>();

                if (callbacks.Contains(callback))
                    throw new InvalidOperationException("This lifecycle callback is already registered.");

                InvocationListCache<Delegate>.Get(callback);
                callbacks.Add(callback);
            }


            public void RemoveLifecycleCallback(Delegate callback, bool disposing)
            {
                var callbacks = disposing ? _onDisposing : _onDisposed;
                if (callbacks == null || !callbacks.Remove(callback))
                    return;

                RemoveIfEmpty();
            }

            public void NotifyDisposing() => Notify(_onDisposing);
            public void NotifyDisposed() => Notify(_onDisposed);

            private void Notify(AdaptiveCollection<Delegate> callbacks)
            {
                if (callbacks == null)
                    return;

                foreach (var registration in callbacks)
                {
                    if (_isDisposed)
                        return;

                    try
                    {
                        foreach (var callback in InvocationListCache<Delegate>.Get(registration))
                        {
                            if (_isDisposed || !callbacks.IsCurrentItemActive)
                                break;

                            if (callback is Action action) action();
                            else ((Action<object>)callback)(_hub.Owner);
                        }
                    }
                    catch (Exception exception)
                    {
                        // A failing lifecycle callback ends the whole connection.
                        Dispose();
                        ExceptionHandler.Report(exception);
                        return;
                    }
                }
            }

            // Removal
            private void RemoveIfEmpty()
            {
                if (IsEmpty)
                    Dispose();
            }

            /// <summary>
            /// Ends the connection: releases its stream subscriptions and callbacks and removes it from the hub.
            /// Later calls do nothing.
            /// </summary>
            public void Dispose()
            {
                if (_isDisposed) return;
                _isDisposed = true;

                foreach (var subscription in _subscriptions)
                    subscription.Handle.Dispose();

                _subscriptions.Clear();
                _onDisposing?.Clear();
                _onDisposed?.Clear();

                _onDisposing = null;
                _onDisposed = null;

                _hub.RemoveConnection(_key, this);
                _key = null;
            }
        }
    }
}
