using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Blackthunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        internal class ParallelBatch
        {
            private readonly T _value;
            private readonly List<Invocation> _pending = new();


            public ParallelBatch(T value) => _value = value;

            public void Add(Subscription subscription) => _pending.Add(new Invocation(subscription, _value));

            public void Flush(ref ExceptionDispatchInfo failure)
            {
                if (_pending.Count == 0) return;
                if (failure != null || EventDispatchScope.Current.Root.Stopped)
                {
                    _pending.Clear();
                    return;
                }

                var invocations = _pending.ToArray();
                _pending.Clear();
                var tasks = new Task[invocations.Length];

                for (int i = 0; i < invocations.Length; i++)
                {
                    var invocation = invocations[i];
                    tasks[i] = invocation.Subscription.Handle.IsDisposed
                        ? Task.CompletedTask
                        : Task.Run(invocation.Run);
                }

                // Wait on the aggregate rather than inline individual callbacks onto the publisher thread.
                Task.WhenAll(tasks).GetAwaiter().GetResult();

                foreach (var invocation in invocations)
                {
                    invocation.Scope.TransferChanges();
                    if (invocation.Error != null) invocation.Fail(invocation.Error, ref failure);
                }
            }
        }

        private class Invocation
        {
            public Subscription Subscription { get; }
            public EventDispatchScope Scope { get; }
            public Exception Error { get; private set; }

            private readonly T _value;
            private readonly EventDispatchScope _owner;


            public Invocation(Subscription subscription, T value)
            {
                Subscription = subscription;
                _value = value;
                _owner = EventDispatchScope.Current;
                Scope = new EventDispatchScope(_owner.Root, worker: true);
            }

            public void Run()
            {
                var previous = EventDispatchScope.Current;
                Scope.Enter(_owner);
                Scope.Receiver = Subscription;

                try
                {
                    Subscription.Publish(_value);
                }
                catch (Exception exception)
                {
                    Error = exception;
                }
                finally
                {
                    EventDispatchScope.Current = previous;
                }
            }

            public void Fail(Exception exception, ref ExceptionDispatchInfo failure)
            {
                try
                {
                    Subscription.Fail(exception, ref failure);
                }
                catch (Exception reportFailure)
                {
                    // A failing removal or reporter must not abandon later callbacks' queued changes.
                    failure ??= ExceptionDispatchInfo.Capture(reportFailure);
                }
            }
        }
    }
}
