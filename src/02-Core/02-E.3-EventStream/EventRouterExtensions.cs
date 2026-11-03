using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core.EventStreamCore;

namespace Blackthunder.UniEngine.Core
{
    public static class EventRouterExtensions
    {
        // Construction
        public static IEventRouter<TEventArgs> Route<TEventArgs>(Func<TEventArgs, uint> toMask = null) =>
            new EventRouter<TEventArgs>(toMask);

        public static IEventRouter<TEventArgs> Route<TEventArgs>(
            this IEventStream<TEventArgs> stream,
            Func<TEventArgs, uint> toMask = null,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            EventStream<TEventArgs>.ThrowIfDisposed(stream);
            return stream.Chain(new EventRouter<TEventArgs>(toMask) { TerminationMode = terminationMode });
        }

        // Connections
        public static TObserver Chain<TEventArgs, TObserver>(
            this IEventRouter<TEventArgs> router, TEventArgs target, TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            ThrowIfCannotChain(router, observer);
            connection = router.Subscribe(target, observer);
            EventStream<TEventArgs>.BindConnection(observer, connection);
            return observer;
        }

        public static TObserver Chain<TEventArgs, TObserver>(
            this IEventRouter<TEventArgs> router, IEnumerable<TEventArgs> targets, TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            ThrowIfCannotChain(router, observer);
            connection = router.Subscribe(targets, observer);
            EventStream<TEventArgs>.BindConnection(observer, connection);
            return observer;
        }

        public static IEventRouter<TEventArgs> Connect<TEventArgs, TObserver>(
            this IEventRouter<TEventArgs> router, TEventArgs target, TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            Chain(router, target, observer, out connection);
            return router;
        }

        public static IEventRouter<TEventArgs> Connect<TEventArgs, TObserver>(
            this IEventRouter<TEventArgs> router, IEnumerable<TEventArgs> targets, TObserver observer, out IDisposable connection)
            where TObserver : IObserver<TEventArgs>
        {
            Chain(router, targets, observer, out connection);
            return router;
        }

        private static void ThrowIfCannotChain<TEventArgs, TObserver>(IEventRouter<TEventArgs> router, TObserver observer)
            where TObserver : IObserver<TEventArgs>
        {
            EventStream<TEventArgs>.ThrowIfDisposed(router);
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));
            if (ReferenceEquals(observer, router))
                throw new ArgumentException("A stream cannot chain itself.", nameof(observer));
        }
    }
}
