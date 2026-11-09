using System;

namespace Blackthunder.UniEngine.Core
{
    public partial class EventStream<T>
    {
        // The entrance of a stream for one upstream. It knows where to connect before the first
        // subscription and keeps the connection afterwards, so the stream can release it.
        internal class InputGate : IGraphObserver<T>, IDisposable
        {
            // Front
            internal bool IsUpstreamDisposed => _upstream.IsDisposed;
            internal int Depth => _upstream.Depth;

            // Internal
            private readonly IRegistrable<T> _upstream;
            private EventStream<T> _stream;
            private IDisposable _connection;
            private bool _finished;


            // Content --------------------
            // Connection
            internal InputGate(IRegistrable<T> upstream = null) => _upstream = upstream;

            internal void Bind(EventStream<T> stream) => _stream = stream;
            internal void Connect() => Keep(_upstream.Register(Registration<T>.Connection(this), null));
            internal void Connect(IObservable<T> source) => Keep(source.Subscribe(this));

            // A source can end before returning its connection.
            private void Keep(IDisposable connection)
            {
                if (_finished) connection?.Dispose();
                else _connection = connection;
            }

            // Upstream signals
            public void OnNext(T value)
            {
                if (!_finished && !_stream._isTerminating) _stream.OnNext(value);
            }

            public void OnCompleted()
            {
                if (_finished) return;
                _finished = true;

                try
                {
                    _stream.CompleteInput();
                }
                finally
                {
                    Release();
                }
            }

            public void OnError(Exception error)
            {
                if (_finished) return;
                _finished = true;

                try
                {
                    _stream.Terminate(error, false);
                }
                finally
                {
                    Release();
                }
            }

            public void OnDisconnected()
            {
                if (_finished) return;
                _finished = true;
                _connection = null;

                _stream.LoseInput();
            }

            // Disposal
            public void Dispose()
            {
                _finished = true;
                Release();
            }

            private void Release()
            {
                var connection = _connection;
                _connection = null;
                connection?.Dispose();
            }
        }
    }
}
