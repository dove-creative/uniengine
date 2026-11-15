using System;

namespace BlackThunder.UniEngine.Core
{
    // What a subscription or a new stream attaches to: a stream itself or a path that starts from it.
    // A path passes registrations to its source, so only the stream at the end touches its graph.
    internal interface IRegistrable<T>
    {
        int Depth { get; }
        bool IsDisposed { get; }

        void ThrowIfDisposed();
        IDisposable Register(Registration<T> registration, PathInfo<T> path);
    }
}
