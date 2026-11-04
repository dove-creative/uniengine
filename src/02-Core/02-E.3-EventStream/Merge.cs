using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Blackthunder.UniEngine.Core.EventStreamCore
{
    public class Merge<TEventArgs> : EventSource<TEventArgs>, IGraphObserver<TEventArgs>
    {
        public enum CompletionMode
        {
            All,
            Any,
        }

        private readonly CompletionMode _completionMode;
        private int _remainingUpstreams;

        public Merge(params IEventSource<TEventArgs>[] upstreams)
            : this(TerminationMode.NoSubscribers, CompletionMode.All, (IEnumerable<IEventSource<TEventArgs>>)upstreams) { }

        public Merge(IEnumerable<IEventSource<TEventArgs>> upstreams)
            : this(TerminationMode.NoSubscribers, CompletionMode.All, upstreams) { }

        public Merge(CompletionMode completionMode, params IEventSource<TEventArgs>[] upstreams)
            : this(TerminationMode.NoSubscribers, completionMode, (IEnumerable<IEventSource<TEventArgs>>)upstreams) { }

        public Merge(CompletionMode completionMode, IEnumerable<IEventSource<TEventArgs>> upstreams)
            : this(TerminationMode.NoSubscribers, completionMode, upstreams) { }

        public Merge(TerminationMode terminationMode, params IEventSource<TEventArgs>[] upstreams)
            : this(terminationMode, CompletionMode.All, (IEnumerable<IEventSource<TEventArgs>>)upstreams) { }

        public Merge(TerminationMode terminationMode, IEnumerable<IEventSource<TEventArgs>> upstreams)
            : this(terminationMode, CompletionMode.All, upstreams) { }

        public Merge(TerminationMode terminationMode, CompletionMode completionMode, params IEventSource<TEventArgs>[] upstreams)
            : this(terminationMode, completionMode, (IEnumerable<IEventSource<TEventArgs>>)upstreams) { }

        public Merge(TerminationMode terminationMode, CompletionMode completionMode, IEnumerable<IEventSource<TEventArgs>> upstreams)
        {
            TerminationMode = terminationMode;
            _completionMode = completionMode;

            if (upstreams == null)
                return;

            try
            {
                // Count every input before subscribing. A source can terminate inside Subscribe.
                var sources = new List<IEventSource<TEventArgs>>();
                foreach (var stream in upstreams)
                {
                    if (stream == null) continue;
                    EventSource<TEventArgs>.ThrowIfDisposed(stream);
                    sources.Add(stream);
                }
                _remainingUpstreams = sources.Count;
                foreach (var source in sources)
                {
                    if (IsDisposed) break;
                    var input = new Input(this);
                    ((IPassiveObserver)this).TakeConnection(input);
                    input.Connect(source);
                }
            }
            catch (Exception exception)
            {
                try
                {
                    Dispose();
                }
                finally
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                throw;
            }
        }

        private void CompleteInput()
        {
            if (IsDisposing) return;

            if (_completionMode == CompletionMode.Any)
            {
                base.OnCompleted();
                return;
            }

            if (_remainingUpstreams > 0)
                _remainingUpstreams--;

            if (_remainingUpstreams <= 0)
                base.OnCompleted();
        }

        void IObserver<TEventArgs>.OnCompleted() => CompleteInput();

        private sealed class Input : IGraphObserver<TEventArgs>, IDisposable
        {
            private readonly Merge<TEventArgs> _merge;
            private IDisposable _connection;
            private bool _finished;

            internal Input(Merge<TEventArgs> merge) => _merge = merge;

            internal void Connect(IObservable<TEventArgs> source)
            {
                var connection = source.Subscribe(this);
                if (_finished) connection?.Dispose();
                else _connection = connection;
            }

            public void OnNext(TEventArgs value)
            {
                if (!_finished) _merge.OnNext(value);
            }

            public void OnCompleted()
            {
                if (_finished) return;
                _finished = true;
                try
                {
                    _merge.CompleteInput();
                }
                finally
                {
                    Dispose();
                }
            }
            public void OnError(Exception error)
            {
                if (_finished) return;
                _finished = true;
                try
                {
                    _merge.OnError(error);
                }
                finally
                {
                    Dispose();
                }
            }
            public void Dispose()
            {
                _finished = true;
                var connection = _connection;
                _connection = null;
                connection?.Dispose();
            }
        }
    }
}
