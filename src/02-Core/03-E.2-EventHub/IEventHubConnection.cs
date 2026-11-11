using System;

namespace Blackthunder.UniEngine.Core
{
    internal enum EventHubUpdateKind { Typed, Object, Parameterless }

    // The builder only needs lifetime validation and registration services.
    internal interface IEventHubConnection<TEventArgs>
    {
        void ThrowIfUnavailable();
        void AddUpdate(Delegate source, EventHubUpdateKind kind, EventCondition<TEventArgs> condition);
        void AddDisposing(Delegate callback);
        void AddDisposed(Delegate callback);
    }
}
