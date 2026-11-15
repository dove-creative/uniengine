namespace BlackThunder.UniEngine.Core
{
    public interface ILifecycleHub
    {
        bool IsDisposed { get; }

        ILifecycleHubBuilder Connect(object listener);
        void Disconnect(object listener);
    }
}
