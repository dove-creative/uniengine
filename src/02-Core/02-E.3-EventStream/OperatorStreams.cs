using System;
using System.Runtime.ExceptionServices;

namespace BlackThunder.UniEngine.Core
{
    internal class TakeStream<T> : EventStream<T>
    {
        internal TakeStream(InputGate input, int count) : base(input) =>
            Inject(new Counter(this, count));

        private class Counter : EventGraph<T>.Node
        {
            private readonly TakeStream<T> _stream;
            private int _remaining;

            internal Counter(TakeStream<T> stream, int count)
            {
                _stream = stream;
                _remaining = count;
            }

            internal override void Visit(
                T value,
                long lastVisible,
                ref ExceptionDispatchInfo failure,
                EventGraph<T>.ParallelBatch batch = null)
            {
                if (_remaining > 0)
                {
                    base.Visit(value, lastVisible, ref failure, batch);
                    _remaining--;
                }

                if (_remaining <= 0) Complete(ref failure);
            }

            private void Complete(ref ExceptionDispatchInfo failure)
            {
                try
                {
                    _stream.OnCompleted();
                }
                catch (Exception exception)
                {
                    // Completion already reported its failure; preserve an earlier value failure.
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
    }

    internal class TakeWhileStream<T> : EventStream<T>
    {
        internal TakeWhileStream(InputGate input, Func<T, bool> predicate) : base(input) =>
            Inject(new Condition(this, predicate));

        private class Condition : EventGraph<T>.Node
        {
            private readonly TakeWhileStream<T> _stream;
            private readonly Func<T, bool> _predicate;

            internal Condition(TakeWhileStream<T> stream, Func<T, bool> predicate)
            {
                _stream = stream;
                _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            }

            internal override void Visit(
                T value,
                long lastVisible,
                ref ExceptionDispatchInfo failure,
                EventGraph<T>.ParallelBatch batch = null)
            {
                if (!_stream.TryEvaluate(_predicate, value, out var matches)) return;

                if (matches) base.Visit(value, lastVisible, ref failure, batch);
                else _stream.OnCompleted();
            }
        }
    }

    internal class SkipStream<T> : EventStream<T>
    {
        internal SkipStream(InputGate input, int count) : base(input) =>
            Inject(new Counter(count));

        private class Counter : EventGraph<T>.Node
        {
            private int _remaining;

            internal Counter(int count) => _remaining = count;

            internal override void Visit(
                T value,
                long lastVisible,
                ref ExceptionDispatchInfo failure,
                EventGraph<T>.ParallelBatch batch = null)
            {
                if (_remaining > 0)
                {
                    _remaining--;
                    return;
                }

                base.Visit(value, lastVisible, ref failure, batch);
            }
        }
    }

    internal class SkipWhileStream<T> : EventStream<T>
    {
        internal SkipWhileStream(InputGate input, Func<T, bool> predicate) : base(input) =>
            Inject(new Condition(this, predicate));

        private class Condition : EventGraph<T>.Node
        {
            private readonly SkipWhileStream<T> _stream;
            private readonly Func<T, bool> _predicate;
            private bool _isOpen;

            internal Condition(SkipWhileStream<T> stream, Func<T, bool> predicate)
            {
                _stream = stream;
                _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            }

            internal override void Visit(
                T value,
                long lastVisible,
                ref ExceptionDispatchInfo failure,
                EventGraph<T>.ParallelBatch batch = null)
            {
                if (!_isOpen)
                {
                    if (!_stream.TryEvaluate(_predicate, value, out var shouldSkip) || shouldSkip)
                        return;

                    _isOpen = true;
                }

                base.Visit(value, lastVisible, ref failure, batch);
            }
        }
    }
}
