using System;

namespace Blackthunder.UniEngine.Core
{
    internal sealed class TakeStream<T> : EventStream<T>
    {
        private int _remaining;

        internal TakeStream(Upstream<T> upstream, int count)
            : base(upstream) => _remaining = count;

        internal override void Publish(T value)
        {
            if (_remaining <= 0)
            {
                OnCompleted();
                return;
            }

            try
            {
                base.Publish(value);
            }
            finally
            {
                if (--_remaining == 0) OnCompleted();
            }
        }
    }

    internal sealed class TakeWhileStream<T> : EventStream<T>
    {
        private readonly Filter<T> _filter;

        internal TakeWhileStream(Upstream<T> upstream, Filter<T> filter)
            : base(upstream) => _filter = filter ?? throw new ArgumentNullException(nameof(filter));

        internal override void Publish(T value)
        {
            if (!TryEvaluate(_filter, value, out var matches)) return;

            if (matches) base.Publish(value);
            else OnCompleted();
        }
    }

    internal sealed class SkipStream<T> : EventStream<T>
    {
        private int _remaining;

        internal SkipStream(Upstream<T> upstream, int count)
            : base(upstream) => _remaining = count;

        internal override void Publish(T value)
        {
            if (_remaining > 0)
            {
                _remaining--;
                return;
            }

            base.Publish(value);
        }
    }

    internal sealed class SkipWhileStream<T> : EventStream<T>
    {
        private readonly Filter<T> _filter;
        private bool _isOpen;

        internal SkipWhileStream(Upstream<T> upstream, Filter<T> filter)
            : base(upstream) => _filter = filter ?? throw new ArgumentNullException(nameof(filter));

        internal override void Publish(T value)
        {
            if (!_isOpen)
            {
                if (!TryEvaluate(_filter, value, out var shouldSkip) || shouldSkip) return;
                _isOpen = true;
            }

            base.Publish(value);
        }
    }
}
