using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core.EventStreamCore
{
    public class Debug<TEventArgs> : EventStream<TEventArgs>
    {
        private readonly string _message;
        private readonly LogLevel _logType;

        public Debug(
            string message = "Debug",
            LogLevel logType = LogLevel.Log,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            _message = message;
            _logType = logType;
            TerminationMode = terminationMode;
        }

        public override void OnNext(TEventArgs args)
        {
            ThrowIfDisposed();
            if (!Enabled) return;

            $"{args}: {_message}".Write(_logType);
            if (IsDisposing) return;
            base.OnNext(args);
        }
    }

    public class Skip<TEventArgs> : EventStream<TEventArgs>
    {
        private int _skipCount;

        public Skip(int count = 1, TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            _skipCount = count;
            TerminationMode = terminationMode;
        }

        public override void OnNext(TEventArgs args)
        {
            ThrowIfDisposed();
            if (!Enabled) return;

            if (_skipCount-- > 0) return;
            base.OnNext(args);
        }
    }

    public class SkipWhile<TEventArgs> : EventStream<TEventArgs>
    {
        private Func<TEventArgs, bool> _selector;

        public SkipWhile(Func<bool> selector, TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            _selector = _ => selector();
            TerminationMode = terminationMode;
        }
        public SkipWhile(Func<TEventArgs, bool> selector, TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            _selector = selector;
            TerminationMode = terminationMode;
        }

        public override void OnNext(TEventArgs args)
        {
            ThrowIfDisposed();
            if (!Enabled) return;

            if (_selector != null)
            {
                var shouldSkip = _selector(args);
                if (IsDisposing || shouldSkip) return;
                _selector = null;
            }

            base.OnNext(args);
        }
    }

    public class Take<TEventArgs> : EventStream<TEventArgs>
    {
        private int _takeCount;

        public Take(int count, TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            _takeCount = count;
            TerminationMode = terminationMode;
        }

        public override void OnNext(TEventArgs args)
        {
            ThrowIfDisposed();
            if (!Enabled) return;

            if (TryTerminate()) return;

            base.OnNext(args);
            if (IsDisposing) return;
            _takeCount--;

            if (TryTerminate()) return;
        }

        private bool TryTerminate()
        {
            var isTerminated = _takeCount <= 0;

            if (isTerminated)
                OnCompleted();

            return isTerminated;
        }
    }

    public class TakeWhile<TEventArgs> : EventStream<TEventArgs>
    {
        private readonly Func<TEventArgs, bool> _selector;

        public TakeWhile(Func<bool> selector, TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            _selector = _ => selector();
            TerminationMode = terminationMode;
        }
        public TakeWhile(Func<TEventArgs, bool> selector, TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            _selector = selector;
            TerminationMode = terminationMode;
        }

        public override void OnNext(TEventArgs args)
        {
            ThrowIfDisposed();
            if (!Enabled) return;

            var shouldTake = _selector(args);
            if (IsDisposing) return;
            if (!shouldTake)
            {
                OnCompleted();
                return;
            }

            base.OnNext(args);
        }
    }

    public class Where<TEventArgs> : EventStream<TEventArgs>
    {
        private readonly Func<TEventArgs, bool> _selector;

        public Where(Func<bool> selector, TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            _selector = _ => selector();
            TerminationMode = terminationMode;
        }
        public Where(Func<TEventArgs, bool> selector, TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            _selector = selector;
            TerminationMode = terminationMode;
        }

        public override void OnNext(TEventArgs args)
        {
            ThrowIfDisposed();
            if (!Enabled) return;

            var matches = _selector(args);
            if (IsDisposing || !matches) return;
            base.OnNext(args);
        }
    }

    public class Merge<TEventArgs> : EventStream<TEventArgs>, IObserver<TEventArgs>
    {
        public enum CompletionMode
        {
            All,
            Any,
        }

        private readonly CompletionMode _completionMode;
        private int _remainingUpstreams;

        public Merge(params IEventStream<TEventArgs>[] upstreams)
            : this(TerminationMode.NoSubscribers, CompletionMode.All, (IEnumerable<IEventStream<TEventArgs>>)upstreams) { }

        public Merge(IEnumerable<IEventStream<TEventArgs>> upstreams)
            : this(TerminationMode.NoSubscribers, CompletionMode.All, upstreams) { }

        public Merge(CompletionMode completionMode, params IEventStream<TEventArgs>[] upstreams)
            : this(TerminationMode.NoSubscribers, completionMode, (IEnumerable<IEventStream<TEventArgs>>)upstreams) { }

        public Merge(CompletionMode completionMode, IEnumerable<IEventStream<TEventArgs>> upstreams)
            : this(TerminationMode.NoSubscribers, completionMode, upstreams) { }

        public Merge(TerminationMode terminationMode, params IEventStream<TEventArgs>[] upstreams)
            : this(terminationMode, CompletionMode.All, (IEnumerable<IEventStream<TEventArgs>>)upstreams) { }

        public Merge(TerminationMode terminationMode, IEnumerable<IEventStream<TEventArgs>> upstreams)
            : this(terminationMode, CompletionMode.All, upstreams) { }

        public Merge(TerminationMode terminationMode, CompletionMode completionMode, params IEventStream<TEventArgs>[] upstreams)
            : this(terminationMode, completionMode, (IEnumerable<IEventStream<TEventArgs>>)upstreams) { }

        public Merge(TerminationMode terminationMode, CompletionMode completionMode, IEnumerable<IEventStream<TEventArgs>> upstreams)
        {
            TerminationMode = terminationMode;
            _completionMode = completionMode;

            if (upstreams == null)
                return;

            try
            {
                foreach (var stream in upstreams)
                {
                    if (stream != null && !stream.IsDisposed)
                    {
                        stream.Connect(this, out _);
                        _remainingUpstreams++;
                    }
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

        public new void OnCompleted()
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

        void IObserver<TEventArgs>.OnCompleted() => OnCompleted();
    }
}
