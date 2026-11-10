using System;

namespace Blackthunder.UniEngine.Core
{
    public interface ILifecycleSubject : IDisposable
    {
        bool IsDisposed { get; }
        ILifecycleHub LifecycleHub { get; }
    }
}
