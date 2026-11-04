using System;

namespace Blackthunder.UniEngine.Core
{
    // A point in another stream's graph that a relation connects to with its first subscription.
    internal sealed class Upstream<T>
    {
        // Front
        internal bool IsDisposed => _stream.IsDisposed;
        internal int Depth => _path?.Depth ?? _stream.UpstreamDepth;

        // Internal
        private readonly EventStream<T> _stream;
        private readonly EventPath<T> _path;


        // Content
        internal Upstream(EventStream<T> stream, EventPath<T> path)
        {
            _stream = stream;
            _path = path;
        }

        internal IDisposable Connect(IGraphObserver<T> input) => _stream.ConnectInput(_path, input);
    }
}
