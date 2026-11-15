using System;

namespace BlackThunder.UniEngine.Core
{
    // What a registration attaches at the end of its path:
    // the callbacks of a subscription, or the input of a stream that connects to this place.
    internal readonly struct Registration<T>
    {
        // Front
        internal Action<T> Updated { get; }
        internal Action Completed { get; }
        internal Action<Exception> Errored { get; }
        internal IGraphObserver<T> Input { get; }
        internal bool IsConnection { get; }
        internal bool IsSequential { get; }


        // Content --------------------
        // Construction
        private Registration(
            Action<T> updated,
            Action completed,
            Action<Exception> errored,
            IGraphObserver<T> input,
            bool isConnection,
            bool isSequential = false)
        {
            Updated = updated;
            Completed = completed;
            Errored = errored;
            Input = input;
            IsConnection = isConnection;
            IsSequential = isSequential;
        }

        internal static Registration<T> Subscription(Action<T> updated, Action completed, Action<Exception> errored) =>
            new(updated, completed, errored, null, false);

        internal static Registration<T> Subscription(IObserver<T> observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            return new(
                observer.OnNext,
                observer.OnCompleted,
                observer.OnError,
                observer as IGraphObserver<T>,
                false,
                observer is EventStream<T>);
        }

        internal static Registration<T> Connection(IGraphObserver<T> input) =>
            new(null, null, null, input, true);
    }
}
