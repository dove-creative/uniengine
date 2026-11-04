using System;

namespace Blackthunder.UniEngine.Core
{
    // These observers finish their traversal and report individual failures before rethrowing.
    internal interface IGraphObserver<T> : IObserver<T>
    {
        void OnDisconnected();
    }
}
