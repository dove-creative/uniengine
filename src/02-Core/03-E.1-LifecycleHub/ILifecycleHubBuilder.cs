using System;

namespace BlackThunder.UniEngine.Core
{
    public interface ILifecycleHubBuilder
    {
        ILifecycleHubBuilder OnDisposed(Action onDisposed);
    }
}
