using System;

namespace Blackthunder.UniEngine.Core
{
    /// <summary>
    /// Registers update conditions and lifecycle callbacks for one listener connection.
    /// </summary>
    public class EventHubBuilder<TEventArgs> : IEventHubBuilder<TEventArgs>
    {
        // Internal
        private readonly IEventHubConnection<TEventArgs> _connection;


        // Content
        internal EventHubBuilder(IEventHubConnection<TEventArgs> connection) => _connection = connection;

        public IEventHubBuilder<TEventArgs> OnUpdated(Action<TEventArgs> onUpdated)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddUpdate(onUpdated, EventHubUpdateKind.Typed, null);
            return this;
        }
        public IEventHubBuilder<TEventArgs> OnUpdated(Action onUpdated)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddUpdate(onUpdated, EventHubUpdateKind.Parameterless, null);
            return this;
        }
        public IEventHubBuilder<TEventArgs> OnUpdatedWhere(
            Func<TEventArgs, bool> condition,
            Action<TEventArgs> onUpdated)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddUpdate(onUpdated, EventHubUpdateKind.Typed, PathInfo<TEventArgs>.Where(condition));
            return this;
        }
        public IEventHubBuilder<TEventArgs> OnUpdatedWhere(Func<TEventArgs, bool> condition, Action onUpdated)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddUpdate(onUpdated, EventHubUpdateKind.Parameterless, PathInfo<TEventArgs>.Where(condition));
            return this;
        }
        public IEventHubBuilder<TEventArgs> OnUpdatedWhen(TEventArgs target, Action<TEventArgs> onUpdated)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddUpdate(onUpdated, EventHubUpdateKind.Typed, PathInfo<TEventArgs>.When(target));
            return this;
        }
        public IEventHubBuilder<TEventArgs> OnUpdatedWhen(TEventArgs target, Action onUpdated)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddUpdate(onUpdated, EventHubUpdateKind.Parameterless, PathInfo<TEventArgs>.When(target));
            return this;
        }

        // Preserve the original object delegate instead of forwarding an adapter as its identity.
        IEventHubBuilder IEventHubBuilder.OnUpdated(Action<object> onUpdated)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddUpdate(onUpdated, EventHubUpdateKind.Object, null);
            return this;
        }
        IEventHubBuilder IEventHubBuilder.OnUpdatedWhere(Func<object, bool> condition, Action<object> onUpdated)
        {
            _connection.ThrowIfUnavailable();

            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            _connection.AddUpdate(
                onUpdated, EventHubUpdateKind.Object, PathInfo<TEventArgs>.Where(value => condition(value)), condition);
            return this;
        }
        IEventHubBuilder IEventHubBuilder.OnUpdatedWhere(Func<object, bool> condition, Action onUpdated)
        {
            _connection.ThrowIfUnavailable();

            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            _connection.AddUpdate(
                onUpdated,
                EventHubUpdateKind.Parameterless,
                PathInfo<TEventArgs>.Where(value => condition(value)),
                condition);
            return this;
        }

        // Flags shortcuts must check the connection before validating or constructing the condition.
        internal IEventHubBuilder<TEventArgs> OnUpdatedForFlags<TFlags>(
            TFlags mask,
            Delegate onUpdated,
            EventHubUpdateKind kind) where TFlags : struct, Enum
        {
            _connection.ThrowIfUnavailable();
            _connection.AddUpdate(onUpdated, kind, PathInfo<TEventArgs>.Flags(mask));
            return this;
        }

        public IEventHubBuilder<TEventArgs> OnDisposing(Action onDisposing)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddDisposing(onDisposing);
            return this;
        }
        public IEventHubBuilder<TEventArgs> OnDisposing(Action<object> onDisposing)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddDisposing(onDisposing);
            return this;
        }
        public IEventHubBuilder<TEventArgs> OnDisposed(Action onDisposed)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddDisposed(onDisposed);
            return this;
        }
        public IEventHubBuilder<TEventArgs> OnDisposed(Action<object> onDisposed)
        {
            _connection.ThrowIfUnavailable();
            _connection.AddDisposed(onDisposed);
            return this;
        }
    }

    public static class EventHubBuilderExtensions
    {
        /// <summary>
        /// Registers values sharing any bit with the flags mask, preserving the original value.
        /// Requires a builder returned by EventHub&lt;ulong&gt; and a nonzero mask from a flags enum.
        /// </summary>
        public static IEventHubBuilder<ulong> OnUpdatedForFlags<TFlags>(
            this IEventHubBuilder<ulong> builder,
            TFlags mask,
            Action<ulong> onUpdated)
            where TFlags : struct, Enum
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (builder is not EventHubBuilder<ulong> hubBuilder)
                throw new NotSupportedException("OnUpdatedForFlags requires a builder created by EventHub<ulong>.");

            return hubBuilder.OnUpdatedForFlags(mask, onUpdated, EventHubUpdateKind.Typed);
        }

        public static IEventHubBuilder<ulong> OnUpdatedForFlags<TFlags>(
            this IEventHubBuilder<ulong> builder,
            TFlags mask,
            Action onUpdated)
            where TFlags : struct, Enum
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (builder is not EventHubBuilder<ulong> hubBuilder)
                throw new NotSupportedException("OnUpdatedForFlags requires a builder created by EventHub<ulong>.");

            return hubBuilder.OnUpdatedForFlags(mask, onUpdated, EventHubUpdateKind.Parameterless);
        }
    }
}
