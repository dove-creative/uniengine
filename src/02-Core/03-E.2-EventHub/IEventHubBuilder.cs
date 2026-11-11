using System;

namespace Blackthunder.UniEngine.Core
{
    public interface IEventHubBuilder : ILifecycleHubBuilder
    {
        IEventHubBuilder OnUpdated(Action<object> onUpdated);
        IEventHubBuilder OnUpdated(Action onUpdated);
        IEventHubBuilder OnUpdated(Func<object, bool> condition, Action<object> onUpdated);
        IEventHubBuilder OnUpdated(Func<object, bool> condition, Action onUpdated);

        new IEventHubBuilder OnDisposed(Action onDisposed);
        IEventHubBuilder OnDisposing(Action onDisposing);
        IEventHubBuilder OnDisposing(Action<object> onDisposing);
        IEventHubBuilder OnDisposed(Action<object> onDisposed);

        ILifecycleHubBuilder ILifecycleHubBuilder.OnDisposed(Action onDisposed) => OnDisposed(onDisposed);
    }

    public interface IEventHubBuilder<TEventArgs> : IEventHubBuilder
    {
        IEventHubBuilder<TEventArgs> OnUpdated(Action<TEventArgs> onUpdated);
        new IEventHubBuilder<TEventArgs> OnUpdated(Action onUpdated);
        IEventHubBuilder<TEventArgs> OnUpdated(TEventArgs target, Action<TEventArgs> onUpdated);
        IEventHubBuilder<TEventArgs> OnUpdated(TEventArgs target, Action onUpdated);
        IEventHubBuilder<TEventArgs> OnUpdated(Func<TEventArgs, bool> condition, Action<TEventArgs> onUpdated);
        IEventHubBuilder<TEventArgs> OnUpdated(Func<TEventArgs, bool> condition, Action onUpdated);
        IEventHubBuilder<TEventArgs> OnUpdated(EventCondition<TEventArgs> condition, Action<TEventArgs> onUpdated);
        IEventHubBuilder<TEventArgs> OnUpdated(EventCondition<TEventArgs> condition, Action onUpdated);

        new IEventHubBuilder<TEventArgs> OnDisposing(Action onDisposing);
        new IEventHubBuilder<TEventArgs> OnDisposing(Action<object> onDisposing);
        new IEventHubBuilder<TEventArgs> OnDisposed(Action onDisposed);
        new IEventHubBuilder<TEventArgs> OnDisposed(Action<object> onDisposed);


        // Forwarding implementations
        // Object registrations have no forwarding default: wrapping them in typed adapters would lose
        // the original delegates and the object callback kind that duplicate detection depends on.
        IEventHubBuilder IEventHubBuilder.OnUpdated(Action onUpdated) => OnUpdated(onUpdated);

        IEventHubBuilder IEventHubBuilder.OnDisposing(Action onDisposing) => OnDisposing(onDisposing);
        IEventHubBuilder IEventHubBuilder.OnDisposing(Action<object> onDisposing) => OnDisposing(onDisposing);
        IEventHubBuilder IEventHubBuilder.OnDisposed(Action onDisposed) => OnDisposed(onDisposed);
        IEventHubBuilder IEventHubBuilder.OnDisposed(Action<object> onDisposed) => OnDisposed(onDisposed);
    }
}
