using System;

namespace BlackThunder.UniEngine.Core
{
    public interface IEventHub : ILifecycleHub
    {
        event Action<object> Updated;
        event Action Disposing;
        event Action Disposed;

        new IEventHubBuilder Connect(object listener);
        ILifecycleHubBuilder ILifecycleHub.Connect(object listener) => Connect(listener);
    }

    public interface IEventHub<TEventArgs> : IEventHub
    {
        new event Action<TEventArgs> Updated;

        new IEventHubBuilder<TEventArgs> Connect(object listener);
        IEventHubBuilder IEventHub.Connect(object listener) => Connect(listener);
    }
}
