using System;

namespace BlackThunder.UniEngine.Core
{
    /// <summary>
    /// Represents an object that holds a generic EventHub.
    /// </summary>
    public interface IEventSubject : ILifecycleSubject
    {
        IEventHub EventHub { get; }

        // Explicit implementations of the ILifecycleSubject property.
        ILifecycleHub ILifecycleSubject.LifecycleHub => EventHub;
    }

    /// <summary>
    /// Represents an object that holds a generic EventHub.
    /// </summary>
    public interface IEventSubject<TEventArgs> : IEventSubject
    {
        new IEventHub<TEventArgs> EventHub { get; }

        // Explicit implementations of the IEventSubject property.
        IEventHub IEventSubject.EventHub => EventHub;
    }
}
