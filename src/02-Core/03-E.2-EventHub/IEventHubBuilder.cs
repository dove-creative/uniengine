using System;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public interface IEventHubBuilder : ILifecycleHubBuilder
    {
        IEventHubBuilder Where(Func<object, bool> predicate);
        IEventHubBuilder Debug(string message = "Debug", LogLevel logType = LogLevel.Log);
        IEventHubBuilder OnUpdated(Action<object> onUpdated);
        IEventHubBuilder OnUpdated(Action onUpdated);

        new IEventHubBuilder OnDisposed(Action onDisposed);
        IEventHubBuilder OnDisposing(Action onDisposing);
        IEventHubBuilder OnDisposing(Action<object> onDisposing);
        IEventHubBuilder OnDisposed(Action<object> onDisposed);

        ILifecycleHubBuilder ILifecycleHubBuilder.OnDisposed(Action onDisposed) => OnDisposed(onDisposed);
    }

    public interface IEventHubBuilder<TEventArgs> : IEventHubBuilder
    {
        IEventHubBuilder<TEventArgs> Where(Func<TEventArgs, bool> predicate);
        new IEventHubBuilder<TEventArgs> Debug(string message = "Debug", LogLevel logType = LogLevel.Log);
        IEventHubBuilder<TEventArgs> OnUpdated(Action<TEventArgs> onUpdated);
        new IEventHubBuilder<TEventArgs> OnUpdated(Action onUpdated);

        new IEventHubBuilder<TEventArgs> OnDisposing(Action onDisposing);
        new IEventHubBuilder<TEventArgs> OnDisposing(Action<object> onDisposing);
        new IEventHubBuilder<TEventArgs> OnDisposed(Action onDisposed);
        new IEventHubBuilder<TEventArgs> OnDisposed(Action<object> onDisposed);


        // Forwarding implementations
        IEventHubBuilder IEventHubBuilder.Where(Func<object, bool> predicate)
        {
            // Let the typed path reject null after checking the registration's lifetime.
            if (predicate == null) return Where((Func<TEventArgs, bool>)null);

            return Where(args => predicate(args));
        }
        IEventHubBuilder IEventHubBuilder.Debug(string message, LogLevel logType) => Debug(message, logType);
        IEventHubBuilder IEventHubBuilder.OnUpdated(Action onUpdated) => OnUpdated(onUpdated);
        IEventHubBuilder IEventHubBuilder.OnUpdated(Action<object> onUpdated)
        {
            if (onUpdated == null) return OnUpdated((Action<TEventArgs>)null);

            Action<TEventArgs> typedAction = null;
            foreach (Action<object> callback in onUpdated.GetInvocationList())
            {
                typedAction += args => callback(args);
            }

            return OnUpdated(typedAction);
        }

        IEventHubBuilder IEventHubBuilder.OnDisposing(Action onDisposing) => OnDisposing(onDisposing);
        IEventHubBuilder IEventHubBuilder.OnDisposing(Action<object> onDisposing) => OnDisposing(onDisposing);
        IEventHubBuilder IEventHubBuilder.OnDisposed(Action onDisposed) => OnDisposed(onDisposed);
        IEventHubBuilder IEventHubBuilder.OnDisposed(Action<object> onDisposed) => OnDisposed(onDisposed);
    }
}
