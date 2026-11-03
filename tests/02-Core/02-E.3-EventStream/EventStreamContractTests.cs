using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;
using Blackthunder.UniEngine.Core.EventStreamCore;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture(typeof(EventStream<int>))]
    [TestFixture(typeof(OrderedEventStream<int, DayOfWeek>))]
    [TestFixture(typeof(EventRouter<int>))]
    public class EventStreamContractTests<TSource> where TSource : class, IEventStream<int>, new()
    {
        // Content --------------------
        // Connection ownership
        // C1: Subscribe leaves handle ownership with the caller.
        [Test]
        public void PlainObserverSubscriptionRemainsUntilCallerReleasesIt()
        {
            using var source = CreateSource().With(TerminationMode.NoSubscribers);
            using var child = new EventStream<int>();
            var values = new List<int>();
            using var listener = child.Subscribe(values.Add);
            using var subscription = source.Subscribe(child);

            source.OnNext(1);
            Assert.That(values, Is.EqualTo(new[] { 1 }));
            child.Dispose();
            Assert.That(source.IsDisposed, Is.False);
            subscription.Dispose();
            Assert.That(source.IsDisposed, Is.True);
        }

        // C2: Chain and Connect return different objects and bind each handle independently.
        [TestCase(false)]
        [TestCase(true)]
        public void ChainAndConnectReturnChildAndSourceAndBindIndependentHandles(bool releaseChain)
        {
            using var source = CreateSource().With(TerminationMode.NoSubscribers);
            using var child = new EventStream<int>();
            var values = new List<int>();
            using var listener = child.Subscribe(values.Add);

            Assert.That(source.Chain(child, out var chained), Is.SameAs(child));
            Assert.That(source.Connect(child, out var connected), Is.SameAs(source));
            source.OnNext(1);
            (releaseChain ? chained : connected).Dispose();
            source.OnNext(2);
            Assert.That(values, Is.EqualTo(new[] { 1, 1, 2 }));

            child.Dispose();
            Assert.That(source.IsDisposed, Is.True);
        }

        // C3: A transfer failure preserves its exception and rolls back the new connection.
        [Test]
        public void FailedHandleTransferRollsBackOnlyTheNewSubscription()
        {
            using var source = CreateSource().With(TerminationMode.NoSubscribers);
            var existingValues = new List<int>();
            using var existing = source.Subscribe(existingValues.Add);
            var observer = new RejectingObserver();

            var failure = Assert.Throws<ApplicationException>(() => source.Chain(observer, out _));
            Assert.That(failure, Is.SameAs(observer.Failure));
            source.OnNext(7);
            Assert.That(existingValues, Is.EqualTo(new[] { 7 }));
            Assert.That(observer.ValueCalls, Is.Zero);
            existing.Dispose();
            Assert.That(source.IsDisposed, Is.True);
        }

        // Termination
        // C4: Disabling values leaves completion and error notifications active.
        [TestCase(false)]
        [TestCase(true)]
        public void DisabledSourceStillForwardsCompletionOrErrorToItsChild(bool sendError)
        {
            using var source = CreateSource();
            using var child = source.Chain<EventStream<int>>();
            var values = new List<int>();
            var completed = 0;
            var errors = new List<Exception>();
            using var listener = child.Subscribe(values.Add, () => completed++, errors.Add);
            var failure = new ApplicationException("source");

            source.OnNext(1);
            source.Enabled = false;
            source.OnNext(2);
            if (sendError) source.OnError(failure);
            else source.OnCompleted();

            Assert.That(values, Is.EqualTo(new[] { 1 }));
            Assert.That(completed, Is.EqualTo(sendError ? 0 : 1));
            Assert.That(errors, Is.EqualTo(sendError ? new Exception[] { failure } : Array.Empty<Exception>()));
            Assert.That(source.IsDisposed, Is.True);
            Assert.That(child.IsDisposed, Is.True);
        }

        // C5: Explicit upstream disposal disconnects the child without terminating it.
        [Test]
        public void DisposingSourceLeavesItsChildUsableWithoutTerminalNotification()
        {
            using var source = CreateSource();
            using var child = source.Chain<EventStream<int>>();
            var values = new List<int>();
            var completed = 0;
            var errors = new List<Exception>();
            using var listener = child.Subscribe(values.Add, () => completed++, errors.Add);

            source.OnNext(1);
            source.Dispose();
            Assert.That(child.IsDisposed, Is.False);
            child.OnNext(2);
            Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(completed, Is.Zero);
            Assert.That(errors, Is.Empty);
        }

        // Helpers
        private static IEventStream<int> CreateSource() => new TSource();

        private class RejectingObserver : IObserver<int>, IPassiveObserver
        {
            public ApplicationException Failure { get; } = new ApplicationException("connection transfer");
            public int ValueCalls { get; private set; }

            public void TakeConnection(IDisposable connection) => throw Failure;
            public void OnNext(int value) => ValueCalls++;
            public void OnCompleted() { }
            public void OnError(Exception exception) { }
        }
    }
}
