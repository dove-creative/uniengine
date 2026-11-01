using System;
using System.Linq;
using Blackthunder.UniEngine.Core.EventStreamCore;
using BlackThunder.UniEngine.Infrastructure;

namespace Blackthunder.UniEngine.Core
{
    public static class EventStreamExtensions
    {
        public static IEventStream<TEventArgs> Append<TEventArgs>(this IEventStream<TEventArgs> stream) =>
            stream.Chain<EventStream<TEventArgs>>();


        public static IEventStream<TEventArgs> Debug<TEventArgs>(
            this IEventStream<TEventArgs> stream,
            string message = "Debug",
            LogLevel logType = LogLevel.Log,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            EventStream<TEventArgs>.ThrowIfDisposed(stream);
            return stream.Chain(new Debug<TEventArgs>(message, logType, terminationMode));
        }


        public static IEventStream<TEventArgs> Skip<TEventArgs>(
            this IEventStream<TEventArgs> stream,
            int count = 1,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            EventStream<TEventArgs>.ThrowIfDisposed(stream);
            return stream.Chain(new Skip<TEventArgs>(count, terminationMode));
        }

        public static IEventStream<TEventArgs> SkipWhile<TEventArgs>(
            this IEventStream<TEventArgs> stream,
            Func<bool> selector,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            EventStream<TEventArgs>.ThrowIfDisposed(stream);
            return stream.Chain(new SkipWhile<TEventArgs>(selector, terminationMode));
        }

        public static IEventStream<TEventArgs> SkipWhile<TEventArgs>(
            this IEventStream<TEventArgs> stream,
            Func<TEventArgs, bool> selector,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            EventStream<TEventArgs>.ThrowIfDisposed(stream);
            return stream.Chain(new SkipWhile<TEventArgs>(selector, terminationMode));
        }


        public static IEventStream<TEventArgs> Take<TEventArgs>(
            this IEventStream<TEventArgs> stream,
            int count = 1,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            EventStream<TEventArgs>.ThrowIfDisposed(stream);
            return stream.Chain(new Take<TEventArgs>(count, terminationMode));
        }

        public static IEventStream<TEventArgs> TakeWhile<TEventArgs>(
            this IEventStream<TEventArgs> stream,
            Func<bool> selector,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            EventStream<TEventArgs>.ThrowIfDisposed(stream);
            return stream.Chain(new TakeWhile<TEventArgs>(selector, terminationMode));
        }

        public static IEventStream<TEventArgs> TakeWhile<TEventArgs>(
            this IEventStream<TEventArgs> stream,
            Func<TEventArgs, bool> selector,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            EventStream<TEventArgs>.ThrowIfDisposed(stream);
            return stream.Chain(new TakeWhile<TEventArgs>(selector, terminationMode));
        }


        public static IEventStream<TEventArgs> Where<TEventArgs>(
            this IEventStream<TEventArgs> stream,
            Func<bool> selector,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            EventStream<TEventArgs>.ThrowIfDisposed(stream);
            return stream.Chain(new Where<TEventArgs>(selector, terminationMode));
        }

        public static IEventStream<TEventArgs> Where<TEventArgs>(
            this IEventStream<TEventArgs> stream,
            Func<TEventArgs, bool> selector,
            TerminationMode terminationMode = TerminationMode.NoSubscribers)
        {
            EventStream<TEventArgs>.ThrowIfDisposed(stream);
            return stream.Chain(new Where<TEventArgs>(selector, terminationMode));
        }


        public static IEventStream<TEventArgs> Merge<TEventArgs>(params IEventStream<TEventArgs>[] upstreams) =>
            new Merge<TEventArgs>(TerminationMode.NoSubscribers, upstreams);

        public static IEventStream<TEventArgs> Merge<TEventArgs>(TerminationMode terminationMode, params IEventStream<TEventArgs>[] upstreams) =>
            new Merge<TEventArgs>(terminationMode, upstreams);

        public static IEventStream<TEventArgs> Merge<TEventArgs>(this IEventStream<TEventArgs> upstream, params IEventStream<TEventArgs>[] upstreams) =>
            new Merge<TEventArgs>(TerminationMode.NoSubscribers, new[] { upstream }.Concat(upstreams));

        public static IEventStream<TEventArgs> Merge<TEventArgs>(
            this IEventStream<TEventArgs> upstream,
            TerminationMode terminationMode,
            params IEventStream<TEventArgs>[] upstreams) =>
            new Merge<TEventArgs>(terminationMode, new[] { upstream }.Concat(upstreams));
    }
}
