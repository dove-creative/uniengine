using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.SpatialSystem
{
    public abstract class SpatialHandle : IDisposable
    {
        // Front
        public bool IsDisposed { get; private set; }
        public bool IsValid
        {
            get
            {
                using var scope = SpatialDispatch.Enter();
                if (IsDisposed || _failing) return false;
                if (!OwnerValid) { Dispose(); return false; }

                bool valid;
                try { valid = _isValid(); }
                catch (SpatialEngineException) { throw; }
                catch (Exception exception)
                {
                    Fail(exception);
                    return false;
                }

                if (!valid) Dispose();
                return !IsDisposed;
            }
        }

        // Control
        public event Action Disposed
        {
            add { ThrowIfInvalid(); _disposed += value; }
            remove => _disposed -= value;
        }

        // Internal
        private Func<bool> _isValid;
        private Action _release;
        private Action _disposed;
        private bool _failing;


        // Content
        protected SpatialHandle(Func<bool> isValid, Action release = null)
        {
            _isValid = isValid ?? throw new ArgumentNullException(nameof(isValid));
            _release = release;
        }

        protected void ThrowIfInvalid()
        {
            if (!IsValid) throw new ObjectDisposedException(GetType().Name);
        }

        protected T ReadBinding<T>(Func<T> read)
        {
            using var scope = SpatialDispatch.Enter();
            ThrowIfInvalid();
            try { return read(); }
            catch (SpatialEngineException) { throw; }
            catch (Exception exception) { Fail(exception); }

            throw SpatialDispatch.MarkHandled(
                new ObjectDisposedException(GetType().Name, "The failed binding has been removed."));
        }

        protected void WriteBinding(Action write)
        {
            ThrowIfInvalid();
            TryRunBinding(write);
        }

        internal bool TryRunBinding(Action action)
        {
            using var scope = SpatialDispatch.Enter();
            if (!IsValid) return false;

            try { action(); return !IsDisposed; }
            catch (SpatialEngineException) { throw; }
            catch (Exception exception) { Fail(exception); return false; }
        }

        internal void Fail(Exception exception, SpatialHandle partner = null)
        {
            if (_failing || SpatialDispatch.IsPropagating(exception))
                ExceptionDispatchInfo.Capture(exception).Throw();
            _failing = true;
            if (partner != null) partner._failing = true;
            var errors = new List<Exception>();
            var callbacks = new List<Action>();
            ExceptionDispatchInfo failure = null;

            try { SpatialDispatch.Report(exception); }
            catch (Exception reported) { failure = ExceptionDispatchInfo.Capture(reported); }
            finally
            {
                Detach(errors, callbacks);
                partner?.Detach(errors, callbacks);
                _failing = false;
                if (partner != null) partner._failing = false;
            }

            if (failure != null) failure.Throw();
            SpatialDispatch.Complete(errors);
            SpatialDispatch.Notify(callbacks);
        }

        public void Dispose()
        {
            using var scope = SpatialDispatch.Enter();
            var errors = new List<Exception>();
            var callbacks = new List<Action>();
            Detach(errors, callbacks);
            SpatialDispatch.Complete(errors);
            SpatialDispatch.Notify(callbacks);
        }

        internal void Detach(List<Exception> errors, List<Action> callbacks)
        {
            if (IsDisposed) return;
            IsDisposed = true;
            var release = _release;
            var disposed = _disposed;
            _release = null;
            _disposed = null;
            _isValid = null;

            // Finish structural cleanup before a reporting failure can stop notification delivery.
            CollectChildren(errors, callbacks);
            SpatialDispatch.Run(OnDispose, errors);
            SpatialDispatch.Run(release, errors);

            if (disposed != null)
            {
                foreach (Action callback in disposed.GetInvocationList()) callbacks.Add(callback);
            }
        }

        protected virtual bool OwnerValid => true;

        internal virtual void CollectChildren(List<Exception> errors, List<Action> callbacks) { }
        protected virtual void OnDispose() { }
    }

    public abstract class SpatialSpace : SpatialHandle
    {
        private readonly HashSet<SpatialHandle> _handles = new();


        protected SpatialSpace(Func<bool> isValid, Action release = null) : base(isValid, release) { }

        internal void Register(SpatialHandle handle)
        {
            ThrowIfInvalid();
            _handles.Add(handle);
        }

        internal void Unregister(SpatialHandle handle)
        {
            _handles.Remove(handle);
            RemoveRelations(handle);
        }

        protected SpatialHandle[] SnapshotHandles()
        {
            var handles = new SpatialHandle[_handles.Count];
            _handles.CopyTo(handles);
            return handles;
        }

        protected virtual void RemoveRelations(SpatialHandle handle) { }

        internal override void CollectChildren(List<Exception> errors, List<Action> callbacks)
        {
            var handles = SnapshotHandles();
            _handles.Clear();
            foreach (var handle in handles) handle.Detach(errors, callbacks);
        }
    }

    public interface ICollisionComponent : IDisposable
    {
        event Action Disposed;
        SpatialSpace Space { get; }
        bool IsValid { get; }
        bool IsDisposed { get; }
    }

    internal static class SpatialDispatch
    {
        [ThreadStatic] private static int _depth;
        [ThreadStatic] private static HashSet<Exception> _propagating;
        [ThreadStatic] private static HashSet<Exception> _handled;

        internal readonly struct Scope : IDisposable
        {
            public void Dispose()
            {
                if (--_depth != 0) return;

                _propagating = null;
                _handled = null;
            }
        }

        public static Scope Enter()
        {
            _depth++;
            return new Scope();
        }

        public static bool IsPropagating(Exception exception) => _propagating?.Contains(exception) == true;

        // Marks an exception that only tells the caller about a failure already reported and excluded.
        public static Exception MarkHandled(Exception exception)
        {
            (_handled ??= new HashSet<Exception>()).Add(exception);
            return exception;
        }

        public static void Report(Exception exception)
        {
            if (IsPropagating(exception)) ExceptionDispatchInfo.Capture(exception).Throw();
            try { ExceptionHandler.Report(exception); }
            catch (Exception failure)
            {
                (_propagating ??= new HashSet<Exception>()).Add(failure);
                throw;
            }
        }

        public static void Run(Action action, List<Exception> errors)
        {
            if (action == null) return;
            try { action(); }
            catch (Exception exception) { errors.Add(exception); }
        }

        public static void Invoke<T>(
            Action<T> handlers, T value, Func<bool> active, Action<Action<T>, Exception> fail)
        {
            if (handlers == null) return;

            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                if (!active()) return;

                try { handler(value); }
                catch (SpatialEngineException) { throw; }
                catch (Exception exception) when (_handled?.Contains(exception) == true) { }
                catch (Exception exception) when (!IsPropagating(exception)) { fail(handler, exception); }
            }
        }

        public static void Complete(List<Exception> errors)
        {
            foreach (var error in errors)
            {
                if (error is SpatialEngineException) ExceptionDispatchInfo.Capture(error).Throw();
                Report(error);
            }
        }

        public static void Notify(List<Action> callbacks)
        {
            foreach (var callback in callbacks)
            {
                try { callback(); }
                catch (Exception exception) { Report(exception); }
            }
        }
    }
}
