using System;

namespace BlackThunder.UniEngine.Core
{
    public interface ILifecycleSubject : IDisposable
    {
        bool IsDisposed { get; }
        ILifecycleHub LifecycleHub { get; }
    }
}
