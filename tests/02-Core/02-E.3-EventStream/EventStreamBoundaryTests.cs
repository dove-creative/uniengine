using System;
using System.Collections.Generic;
using System.Threading;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamBoundaryTests
    {
        private enum Phase { First, Last }

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        [TestCase(false)]
        [TestCase(true)]
        public void CurrentOrderRemovalWaitsForBothModesButSkipsTheLaterOrder(bool parallel)
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            Handle current = null;
            Handle later = null;
            var first = parallel ? stream.ForOrderAsync(Phase.First) : stream.ForOrder(Phase.First);
            first.Subscribe(value =>
            {
                if (value != 1) return;
                current.Dispose();
                later.Dispose();
                Assert.That(current.IsDisposed, Is.False);
                Assert.That(later.IsDisposed, Is.False);
            });
            current = (Handle)stream.ForOrder(Phase.First).Where(_ => true).Subscribe(_ => calls.Add("current"));
            later = (Handle)stream.ForOrder(Phase.Last).Subscribe(_ => calls.Add("later"));

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "current" }));
            Assert.That(current.IsDisposed && later.IsDisposed, Is.True);
            stream.OnNext(2);
            Assert.That(calls, Has.Count.EqualTo(1));
        }

        [Test]
        public void RemovingAnAncestorSubscriptionWaitsForTheAncestorOrder()
        {
            using var stream = new EventStream<int>();
            var outer = stream.ForOrderAsync(Phase.First);
            var calls = new List<string>();
            Handle sibling = null;
            outer.ForOrder(Phase.First).Subscribe(_ => sibling.Dispose());
            outer.ForOrder(Phase.Last).Subscribe(_ => Assert.That(sibling.IsDisposed, Is.False));
            sibling = (Handle)outer.Where(_ => true).Subscribe(_ => calls.Add("outer"));
            stream.ForOrder(Phase.Last).Subscribe(_ => Assert.That(sibling.IsDisposed, Is.True));

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "outer" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdditionsWaitForTheNextPublicationAndCancelledHandlesNeverBind(bool parallel)
        {
            using var stream = new EventStream<int>();
            var calls = new List<int>();
            Handle pending = null;
            Handle cancelled = null;
            var first = parallel ? stream.ForOrderAsync(Phase.First) : stream.ForOrder(Phase.First);
            first.Subscribe(value =>
            {
                if (value != 1) return;
                pending = (Handle)stream.ForOrder(Phase.Last).Subscribe(calls.Add);
                cancelled = (Handle)stream.ForOrder(Phase.Last).Subscribe(_ => Assert.Fail("Cancelled registration"));
                cancelled.Dispose();
            });
            stream.ForOrder(Phase.Last).Subscribe(_ => Assert.That(pending.IsDisposed, Is.False));

            stream.OnNext(1);
            Assert.That(calls, Is.Empty);
            Assert.That(cancelled.IsDisposed, Is.True);
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void RootChangesRemainPendingAcrossItsNestedOrders()
        {
            using var stream = new EventStream<int>();
            Handle later = null;
            stream.Subscribe(_ => later.Dispose());
            later = (Handle)stream.ForOrder(Phase.Last).Subscribe(_ => Assert.That(later.IsDisposed, Is.False));
            stream.OnNext(1);
            Assert.That(later.IsDisposed, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisposeFinishesBothModesAndRejectsNewWorkBeforeCleanup(bool parallel)
        {
            using var stream = new EventStream<int>();
            var calls = 0;
            var first = parallel ? stream.ForOrderAsync(Phase.First) : stream.ForOrder(Phase.First);
            first.Subscribe(_ =>
            {
                stream.Dispose();
                Assert.That(stream.IsDisposed, Is.False);
                Assert.Throws<ObjectDisposedException>(() => stream.Subscribe(_ => { }));
                Assert.Throws<ObjectDisposedException>(() => stream.OnNext(2));
            });
            stream.ForOrder(Phase.First).Where(_ => true).Subscribe(_ => calls++);
            stream.ForOrder(Phase.Last).Subscribe(_ => Assert.Fail("Later order"));

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(stream.IsDisposed, Is.True);
        }

        [Test]
        public void NestedDisposeFinishesTheInnermostOrderThenUnwindsTheOuterOrder()
        {
            using var stream = new EventStream<int>();
            var inner = stream.ForOrderAsync(Phase.First);
            var calls = 0;
            inner.ForOrderAsync(Phase.First).Subscribe(_ => stream.Dispose());
            inner.ForOrder(Phase.First).Subscribe(_ => calls++);
            inner.ForOrder(Phase.Last).Subscribe(_ => Assert.Fail("Next inner order"));
            stream.ForOrder(Phase.First).Subscribe(_ => Assert.Fail("Outer remainder"));

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(stream.IsDisposed, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnhandledFailureStopsNewCallsButCommitsAlreadyRequestedChanges(bool parallel)
        {
            using var stream = new EventStream<int>();
            using var target = new EventStream<string>();
            var exception = new InvalidOperationException("receiver");
            Handle added = null;
            var first = parallel ? stream.ForOrderAsync(Phase.First) : stream.ForOrder(Phase.First);
            first.Subscribe(_ =>
            {
                added = (Handle)target.Subscribe(_ => { });
                target.Enabled = false;
                throw exception;
            });
            if (parallel)
                stream.ForOrder(Phase.First).Subscribe(_ => Assert.Fail("Same order remainder"));
            stream.ForOrder(Phase.Last).Subscribe(_ => Assert.Fail("Next order"));

            Assert.That(Assert.Throws<InvalidOperationException>(() => stream.OnNext(1)), Is.SameAs(exception));
            Assert.That(added, Is.Not.Null);
            Assert.That(added.IsDisposed, Is.False);
            Assert.That(target.Enabled, Is.False);
        }

        [Test]
        public void AConnectionJoinDoesNotCommitOuterChanges()
        {
            using var stream = new EventStream<int>();
            var outer = stream.ForOrderAsync(Phase.First);
            using var branch = outer.Branch();
            Handle sameOrder = null;
            var calls = 0;
            outer.Subscribe(_ => sameOrder.Dispose());
            branch.Subscribe(_ => Assert.That(sameOrder.IsDisposed, Is.False));
            sameOrder = (Handle)stream.ForOrder(Phase.First).Subscribe(_ => calls++);

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(sameOrder.IsDisposed, Is.True);
        }

        [Test]
        public void ExplicitPublicationOnAWorkerDefersChangesToItsActiveAncestor()
        {
            using var source = new EventStream<int>();
            using var other = new EventStream<string>();
            Handle ancestor = null;
            var calls = 0;
            other.Subscribe(_ => ancestor.Dispose());
            source.ForOrderAsync(Phase.First).Subscribe(_ => other.OnNext("nested"));
            ancestor = (Handle)source.ForOrder(Phase.First).Subscribe(_ => Interlocked.Increment(ref calls));
            source.ForOrder(Phase.Last).Subscribe(_ => Assert.That(ancestor.IsDisposed, Is.True));

            source.OnNext(1);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void DirectStreamObserverDoesNotReportAnAdmittedFailureTwice()
        {
            using var source = new EventStream<int>();
            using var target = new EventStream<int>();
            var error = new InvalidOperationException("target");
            var reports = new List<Exception>();
            var calls = 0;
            ExceptionHandler.SetHandler(exception => { reports.Add(exception); throw exception; });
            target.Subscribe(_ => throw error);
            var connection = (Handle)source.ForOrder(Phase.First).Subscribe(target);
            source.ForOrder(Phase.Last).Subscribe(_ => calls++);

            Assert.That(Assert.Throws<InvalidOperationException>(() => source.OnNext(1)), Is.SameAs(error));
            Assert.That(reports, Is.EqualTo(new[] { error }));
            Assert.That(connection.IsDisposed, Is.False);
            Assert.That(calls, Is.Zero);
            source.OnNext(2);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void EnabledChangesOnTheActiveRootWaitForItsEnd()
        {
            using var stream = new EventStream<int>();
            var calls = 0;
            stream.ForOrder(Phase.First).Subscribe(_ => stream.Enabled = false);
            stream.ForOrder(Phase.Last).Subscribe(_ =>
            {
                Assert.That(stream.Enabled, Is.True);
                calls++;
            });

            stream.OnNext(1);
            Assert.That(stream.Enabled, Is.False);
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
