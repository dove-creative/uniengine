using System;

namespace Blackthunder.UniEngine.Core
{
    public partial class LifecycleHub
    {
        private class Builder : ILifecycleHubBuilder
        {
            private readonly LifecycleHub _owner;
            private readonly object _client;

            public Builder(LifecycleHub owner, object client)
            {
                _owner = owner;
                _client = client;
            }

            public ILifecycleHubBuilder OnDisposed(Action onDisposed)
            {
                _owner.ThrowIfDisposed();
                _owner._onDisposedClients.Add(_client, onDisposed?.GetInvocationList());
                return this;
            }
        }
    }
}
