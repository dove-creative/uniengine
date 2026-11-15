using System;

namespace BlackThunder.UniEngine.Core
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
                _owner.Register(_client, onDisposed);
                return this;
            }
        }
    }
}
