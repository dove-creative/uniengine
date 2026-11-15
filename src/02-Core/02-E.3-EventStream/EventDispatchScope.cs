using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.Core
{
    internal interface IEventFailureHandler
    {
        void Fail(Exception exception, ref ExceptionDispatchInfo failure);
    }

    internal interface IEventChange
    {
        void Apply();
    }

    // Roots and numeric orders reuse their scopes; ordinary publication allocates no queue.
    internal class EventDispatchScope
    {
        [ThreadStatic] private static EventDispatchScope _current;
        [ThreadStatic] private static bool _unwinding;
        private Change _firstChange;
        private bool _hasChange;
        private List<Change> _changes;
        private volatile bool _stopRequested;

        public static EventDispatchScope Current { get => _current; set => _current = value; }
        public static bool IsUnwinding => _unwinding;
        public EventDispatchScope Root { get; }
        public EventDispatchScope Parent { get; private set; }
        public IEventFailureHandler Receiver { get; set; }
        public bool IsActive { get; private set; }
        public bool IsWorker { get; }
        public bool Stopped { get; private set; }

        public EventDispatchScope(EventDispatchScope root = null, bool worker = false)
        {
            Root = root ?? this;
            IsWorker = worker;
        }

        public void Enter(EventDispatchScope parent = null)
        {
            Parent = parent ?? Current;
            IsActive = true;
            _stopRequested = false;
            Stopped = false;
            Current = this;
        }

        // Ancestor changes wait for their owner. A worker, including its explicit nested
        // publications, must never write directly into a publisher's queue.
        public static EventDispatchScope FindDeferral(EventDispatchScope target)
        {
            var current = Current;
            if (current == null) return target?.IsActive == true ? target : null;

            var destination = target?.IsActive == true ? target : current.IsActive ? current : null;
            if (destination == null) return null;

            for (var scope = current; scope != null; scope = scope.Parent)
            {
                if (scope.IsWorker) return scope;
                if (scope == destination) break;
            }
            return destination;
        }

        public void Defer(EventDispatchScope target, Action apply) =>
            Add(new Change(target, apply, null, Current?.Receiver));
        public void Defer(EventDispatchScope target, IEventChange apply) =>
            Add(new Change(target, null, apply, Current?.Receiver));

        private void Add(Change change)
        {
            if (!_hasChange)
            {
                _firstChange = change;
                _hasChange = true;
            }
            else (_changes ??= new List<Change>()).Add(change);
        }

        public void RequestStop()
        {
            for (var scope = Current; scope != null; scope = scope.Parent)
            {
                if (scope.Root != this || scope.IsWorker || !scope.IsActive) continue;
                scope._stopRequested = true;
                return;
            }
        }

        public void Exit(ref ExceptionDispatchInfo failure)
        {
            IsActive = false;
            if (_hasChange) ExitWithChanges(ref failure);
            else Leave();
        }

        private void Leave()
        {
            if (_stopRequested) Root.Stopped = true;
            Current = Parent;
            Parent = null;
            Receiver = null;
        }

        private void ExitWithChanges(ref ExceptionDispatchInfo failure)
        {
            // Cleanup can enqueue work on an active ancestor, but cannot requeue onto us.
            Current = this;
            var unwinding = _unwinding;
            try
            {
                _unwinding |= failure != null;
                if (_hasChange)
                {
                    _firstChange.Apply(ref failure);
                    _firstChange = default;
                    _hasChange = false;
                }
                if (_changes != null)
                {
                    foreach (var change in _changes) change.Apply(ref failure);
                    _changes.Clear();
                }
            }
            finally
            {
                Leave();
                _unwinding = unwinding;
            }
        }

        public void TransferChanges()
        {
            if (_hasChange)
            {
                FindDeferral(_firstChange.Target).Add(_firstChange);
                _firstChange = default;
                _hasChange = false;
            }
            if (_changes != null)
            {
                foreach (var change in _changes)
                {
                    var destination = FindDeferral(change.Target);
                    // A worker is always joined inside the publishing order.
                    destination.Add(change);
                }
                _changes.Clear();
            }
            Parent = null;
            Receiver = null;
            IsActive = false;
        }

        private readonly struct Change
        {
            public EventDispatchScope Target { get; }
            private readonly Action _apply;
            private readonly IEventChange _change;
            private readonly IEventFailureHandler _receiver;

            public Change(EventDispatchScope target, Action apply, IEventChange change, IEventFailureHandler receiver)
            {
                Target = target;
                _apply = apply;
                _change = change;
                _receiver = receiver;
            }

            public void Apply(ref ExceptionDispatchInfo failure)
            {
                _unwinding |= failure != null;
                try
                {
                    if (_change != null) _change.Apply();
                    else _apply();
                }
                catch (Exception exception)
                {
                    if (_change != null)
                    {
                        // Lifecycle and handle operations already report their own errors.
                        failure ??= ExceptionDispatchInfo.Capture(exception);
                        return;
                    }
                    try
                    {
                        if (_receiver != null) _receiver.Fail(exception, ref failure);
                        else ExceptionHandler.Report(exception);
                    }
                    catch (Exception reportFailure)
                    {
                        failure ??= ExceptionDispatchInfo.Capture(reportFailure);
                    }
                }
            }
        }
    }

    // The same Handle is returned before registration and attached at the commit boundary.
    internal class EventSubscriptionHandle : Handle, IEventChange
    {
        private EventDispatchScope _scope;
        private bool _bound;
        private int _disposeRequested;

        public EventSubscriptionHandle(EventDispatchScope scope) => _scope = scope;

        public void Bind(EventDispatchScope scope, Action removed)
        {
            _scope = scope;
            _bound = true;
            Disposed += removed;
        }

        public override void Dispose()
        {
            if (IsDisposed || Interlocked.Exchange(ref _disposeRequested, 1) != 0) return;
            var deferred = _bound ? EventDispatchScope.FindDeferral(_scope) : null;
            if (deferred != null) deferred.Defer(_scope, this);
            else DisposeImmediately();
        }

        private void DisposeImmediately()
        {
            try { base.Dispose(); }
            finally { _scope = null; }
        }

        void IEventChange.Apply() => DisposeImmediately();
    }
}
