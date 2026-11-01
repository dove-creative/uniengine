using System;

namespace Blackthunder.UniEngine.Core
{
    public interface IAcquirable<THandle> where THandle : IDisposable
    {
        bool IsAcquired { get; }

        THandle Acquire();
        bool TryAcquire(out THandle proxy);
    }

    public class AlreadyAcquiredException : InvalidOperationException
    {
        public AlreadyAcquiredException()
            : base("Cannot acquire an object that has already been acquired.") { }
        public AlreadyAcquiredException(string message)
            : base(message) { }
        public AlreadyAcquiredException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
