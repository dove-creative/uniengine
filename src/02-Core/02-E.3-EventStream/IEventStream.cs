using System;
using Blackthunder.UniEngine.Core.EventStreamCore;

namespace Blackthunder.UniEngine.Core
{
    public interface IEventStream<TEventArgs> :
        IObservable<TEventArgs>,
        IObserver<TEventArgs>,
        IDisposable
    {
        bool Enabled { get; set; }
        TerminationMode TerminationMode { get; set; }
        bool IsDisposed { get; }

        IEventStream<TEventArgs> With(TerminationMode terminationMode);

        IDisposable Subscribe(Action<TEventArgs> updated);
        IDisposable Subscribe(Action<TEventArgs> updated, Action completed, Action<Exception> errored);

        TObserver Chain<TObserver>() where TObserver : IObserver<TEventArgs>, new()
        {
            EventStream<TEventArgs>.ThrowIfDisposed(this);
            return Chain(new TObserver(), out _);
        }

        TObserver Chain<TObserver>(out IDisposable connection) where TObserver : IObserver<TEventArgs>, new()
        {
            EventStream<TEventArgs>.ThrowIfDisposed(this);
            return Chain(new TObserver(), out connection);
        }

        TObserver Chain<TObserver>(TObserver observer) where TObserver : IObserver<TEventArgs> => Chain(observer, out _);
        TObserver Chain<TObserver>(TObserver observer, out IDisposable connection) where TObserver : IObserver<TEventArgs>;

        IEventStream<TEventArgs> Connect<TObserver>() where TObserver : IObserver<TEventArgs>, new()
        {
            EventStream<TEventArgs>.ThrowIfDisposed(this);
            return Connect(new TObserver(), out _);
        }

        IEventStream<TEventArgs> Connect<TObserver>(out IDisposable connection) where TObserver : IObserver<TEventArgs>, new()
        {
            EventStream<TEventArgs>.ThrowIfDisposed(this);
            return Connect(new TObserver(), out connection);
        }

        IEventStream<TEventArgs> Connect<TObserver>(TObserver observer) where TObserver : IObserver<TEventArgs> => Connect(observer, out _);
        IEventStream<TEventArgs> Connect<TObserver>(TObserver observer, out IDisposable connection) where TObserver : IObserver<TEventArgs>;
    }

    /// <summary>
    /// Defines an object that must receive external connection handles to bind those connections to its own lifetime.
    /// </summary>
    public interface IPassiveObserver
    {
        /// <summary>
        /// Stores an externally created upstream connection so the observer can release it during disposal.
        /// </summary>
        /// <remarks>
        /// This method is invoked by IEventStream connection plumbing when an observer is chained or connected.
        /// User code does not need to call it directly because connection ownership is managed by IEventStream.
        /// </remarks>
        void TakeConnection(IDisposable connection);
    }
}
