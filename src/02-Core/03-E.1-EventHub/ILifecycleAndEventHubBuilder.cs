using System;

namespace Blackthunder.UniEngine.Core
{
    public interface ILifecycleHubBuilder
    {
        ILifecycleHubBuilder OnDisposed(Action onDisposed);
    }

    public interface IEventHubBuilder : ILifecycleHubBuilder
    {
        IEventHubBuilder OnUpdated(Action<object> onUpdated);
        IEventHubBuilder OnUpdated(object target, Action<object> onUpdated);
        IEventHubBuilder OnUpdated(object target, Action onUpdated);
        IEventHubBuilder OnUpdated(Func<object, bool> filter, Action<object> onUpdated);
        IEventHubBuilder OnUpdated(Func<object, bool> filter, Action onUpdated);

        new IEventHubBuilder OnDisposed(Action onDisposed);
        IEventHubBuilder OnDisposing(Action onDisposing);
        IEventHubBuilder OnDisposing(Action<object> onDisposing);
        IEventHubBuilder OnDisposed(Action<object> onDisposed);

        ILifecycleHubBuilder ILifecycleHubBuilder.OnDisposed(Action onDisposed) => OnDisposed(onDisposed);
    }

    public interface IEventHubBuilder<TEventArgs> : IEventHubBuilder
    {
        IEventHubBuilder<TEventArgs> OnUpdated(Action<TEventArgs> onUpdated);
        IEventHubBuilder<TEventArgs> OnUpdated(TEventArgs target, Action<TEventArgs> onUpdated);
        IEventHubBuilder<TEventArgs> OnUpdated(TEventArgs target, Action onUpdated);
        IEventHubBuilder<TEventArgs> OnUpdated(Func<TEventArgs, bool> filter, Action<TEventArgs> onUpdated);
        IEventHubBuilder<TEventArgs> OnUpdated(Func<TEventArgs, bool> filter, Action onUpdated);

        new IEventHubBuilder<TEventArgs> OnDisposing(Action onDisposing);
        new IEventHubBuilder<TEventArgs> OnDisposing(Action<object> onDisposing);
        new IEventHubBuilder<TEventArgs> OnDisposed(Action onDisposed);
        new IEventHubBuilder<TEventArgs> OnDisposed(Action<object> onDisposed);


        // Forwarding implementations
        IEventHubBuilder IEventHubBuilder.OnUpdated(Action<object> onUpdated)
        {
            if (onUpdated == null) return OnUpdated((Action<TEventArgs>)null);

            Action<TEventArgs> typedAction = null;
            foreach (var entry in onUpdated.GetInvocationList())
            {
                var callback = (Action<object>)entry;
                typedAction += args => callback(args);
            }

            return OnUpdated(typedAction);
        }
        IEventHubBuilder IEventHubBuilder.OnUpdated(object target, Action<object> onUpdated)
        {
            if (target != null && target is not TEventArgs)
                throw new ArgumentException(
                    $"The provided target object is not compatible with the event args type '{typeof(TEventArgs).Name}'. Expected '{typeof(TEventArgs).Name}', but got '{target.GetType().FullName}'.",
                    nameof(target));

            var typedTarget = (TEventArgs)target;

            if (onUpdated == null) return OnUpdated(typedTarget, (Action<TEventArgs>)null);

            Action<TEventArgs> typedAction = null;
            foreach (var entry in onUpdated.GetInvocationList())
            {
                var callback = (Action<object>)entry;
                typedAction += args => callback(args);
            }

            return OnUpdated(typedTarget, typedAction);
        }
        IEventHubBuilder IEventHubBuilder.OnUpdated(object target, Action onUpdated)
        {
            if (target != null && target is not TEventArgs)
                throw new ArgumentException(
                    $"The provided target object is not compatible with the event args type '{typeof(TEventArgs).Name}'. Expected '{typeof(TEventArgs).Name}', but got '{target.GetType().FullName}'.",
                    nameof(target));

            var typedTarget = (TEventArgs)target;

            return OnUpdated(typedTarget, onUpdated);
        }
        IEventHubBuilder IEventHubBuilder.OnUpdated(Func<object, bool> filter, Action<object> onUpdated)
        {
            Func<TEventArgs, bool> typedFilter = null;
            if (filter != null) typedFilter = args => filter(args);

            if (onUpdated == null) return OnUpdated(typedFilter, (Action<TEventArgs>)null);

            Action<TEventArgs> typedAction = null;
            foreach (var entry in onUpdated.GetInvocationList())
            {
                var callback = (Action<object>)entry;
                typedAction += args => callback(args);
            }

            return OnUpdated(typedFilter, typedAction);
        }
        IEventHubBuilder IEventHubBuilder.OnUpdated(Func<object, bool> filter, Action onUpdated)
        {
            Func<TEventArgs, bool> typedFilter = null;
            if (filter != null) typedFilter = args => filter(args);

            return OnUpdated(typedFilter, onUpdated);
        }

        IEventHubBuilder IEventHubBuilder.OnDisposing(Action onDisposing) => OnDisposing(onDisposing);
        IEventHubBuilder IEventHubBuilder.OnDisposing(Action<object> onDisposing) => OnDisposing(onDisposing);
        IEventHubBuilder IEventHubBuilder.OnDisposed(Action onDisposed) => OnDisposed(onDisposed);
        IEventHubBuilder IEventHubBuilder.OnDisposed(Action<object> onDisposed) => OnDisposed(onDisposed);
    }
}
