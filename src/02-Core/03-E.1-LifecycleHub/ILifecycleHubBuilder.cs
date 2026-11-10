using System;

namespace Blackthunder.UniEngine.Core
{
    public interface ILifecycleHubBuilder
    {
        ILifecycleHubBuilder OnDisposed(Action onDisposed);
    }
}
