using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamParallelTests
    {
        private enum Phase { First, Last }
        private enum WideOrder : long { TooLarge = (long)int.MaxValue + 1 }
        [Flags] private enum Kind { Selected = 1 }
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        [Test]
        public void WorkersOverlapAndPublicationWaitsBeforeTheSequentialGroup()
        {
            using var stream = new EventStream<int>();
            using var started = new CountdownEvent(2);
            using var release = new ManualResetEventSlim();
            var workers = new ConcurrentBag<int>();
            var publisher = 0;
            var sequential = 0;

            for (int i = 0; i < 2; i++)
            {
                stream.ForOrderAsync(Phase.First).Subscribe(_ =>
                {
                    workers.Add(Thread.CurrentThread.ManagedThreadId);
                    started.Signal();
                    Assert.That(release.Wait(Timeout), Is.True);
                });
            }

            stream.ForOrder(Phase.First).Subscribe(_ => sequential = Thread.CurrentThread.ManagedThreadId);
            var publication = Task.Factory.StartNew(() =>
            {
                publisher = Thread.CurrentThread.ManagedThreadId;
                stream.OnNext(1);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            try
            {
                Assert.That(started.Wait(Timeout), Is.True);
                Assert.That(publication.IsCompleted, Is.False);
                Assert.That(Volatile.Read(ref sequential), Is.Zero);
                Assert.That(workers, Has.None.EqualTo(publisher));
            }
            finally
            {
                release.Set();
                Assert.That(publication.Wait(Timeout), Is.True);
            }

            Assert.That(sequential, Is.EqualTo(publisher));
        }

        [Test]
        public void OrderValuePrecedesModeRegardlessOfRegistrationAndRecreationOrder()
        {
            using var stream = new EventStream<int>();
            var calls = new ConcurrentQueue<string>();
            stream.ForOrder(Phase.Last).Subscribe(_ => calls.Enqueue("sync-last"));
            stream.ForOrderAsync(Phase.Last).Subscribe(_ => calls.Enqueue("async-last"));
            stream.ForOrder(Phase.First).Subscribe(_ => calls.Enqueue("sync-first"));
            var first = stream.ForOrderAsync(Phase.First).Subscribe(_ => calls.Enqueue("old"));
            first.Dispose();
            stream.ForOrderAsync(Phase.First).Subscribe(_ => calls.Enqueue("async-first"));
            stream.Subscribe(_ => calls.Enqueue("plain"));

            stream.OnNext(1);

            Assert.That(calls.Count(value => value == "plain"), Is.EqualTo(1));
            Assert.That(calls.Where(value => value != "plain"),
                Is.EqualTo(new[] { "async-first", "sync-first", "async-last", "sync-last" }));
        }

        [Test]
        public void NestedOrdersAndStreamConnectionsAreBoundariesOnThePublisherThread()
        {
            using var stream = new EventStream<int>();
            var path = stream.ForOrderAsync(Phase.First);
            var calls = new ConcurrentQueue<string>();
            var publisher = Thread.CurrentThread.ManagedThreadId;
            path.Subscribe(_ => calls.Enqueue("outer"));
            path.ForOrder(Phase.First).Subscribe(_ =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(publisher));
                calls.Enqueue("nested-sync");
            });
            path.ForOrderAsync(Phase.First).Subscribe(_ => calls.Enqueue("nested-async"));
            using var first = path.Branch();
            first.Subscribe(_ =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(publisher));
                calls.Enqueue("branch-sync");
            });
            first.ForOrderAsync(Phase.First).Subscribe(_ => calls.Enqueue("branch-async"));
            using var second = path.Take(1);
            second.Subscribe(_ => calls.Enqueue("take"));
            path.Where(_ => true).Subscribe(_ => calls.Enqueue("outer-tail"));

            stream.OnNext(1);

            Assert.That(calls, Is.EquivalentTo(new[]
            {
                "outer", "nested-async", "nested-sync", "branch-sync", "branch-async", "take", "outer-tail",
            }));
            Assert.That(second.IsDisposed, Is.True);
        }

        [Test]
        public void DirectObserverSubscriptionIsASequentialStreamBoundary()
        {
            using var source = new EventStream<int>();
            using var target = new EventStream<int>();
            var publisher = Thread.CurrentThread.ManagedThreadId;
            var calls = new ConcurrentQueue<string>();
            var path = source.ForOrderAsync(Phase.First);
            path.Subscribe(_ => calls.Enqueue("before"));
            target.Subscribe(_ =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(publisher));
                calls.Enqueue("target");
            });
            path.Subscribe(target);
            path.Subscribe(_ => calls.Enqueue("after"));

            source.OnNext(1);

            Assert.That(calls, Is.EqualTo(new[] { "before", "target", "after" }));
        }

        [Test]
        public void FiltersRunBeforeWorkersAndAreSharedOnlyWithinTheSameMode()
        {
            using var stream = new EventStream<ulong>();
            var publisher = Thread.CurrentThread.ManagedThreadId;
            var checks = 0;
            var calls = 0;
            Func<ulong, bool> selected = value =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(publisher));
                checks++;
                return value == 1;
            };

            for (int i = 0; i < 2; i++)
            {
                stream.ForOrderAsync(Phase.First).Where(selected).ForFlags(Kind.Selected)
                    .Subscribe(_ => Interlocked.Increment(ref calls));
            }

            stream.ForOrder(Phase.First).Where(selected).Subscribe(_ => calls++);
            stream.OnNext(0);
            stream.OnNext(1);

            Assert.That(checks, Is.EqualTo(4));
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void MulticastSelfRemovalWaitsUntilItsWholeInvocationFinishes()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            IDisposable subscription = null;
            Action<int> callback = _ =>
            {
                calls.Add("head");
                subscription.Dispose();
            };
            callback += _ => calls.Add("tail");
            subscription = stream.ForOrderAsync(Phase.First).Subscribe(callback);

            stream.OnNext(1);
            stream.OnNext(2);

            Assert.That(calls, Is.EqualTo(new[] { "head", "tail" }));
            Assert.That(stream.NodeCount, Is.Zero);
        }

        [Test]
        public void DeferredChangesSkipLaterGroupsAndNewRegistrationsWaitForTheNextValue()
        {
            using var stream = new EventStream<int>();
            var calls = new ConcurrentQueue<string>();
            IDisposable removed = null;
            IDisposable pending = null;
            var path = stream.ForOrderAsync(Phase.First);
            path.Subscribe(value =>
            {
                if (value != 1) return;

                removed.Dispose();
                pending = stream.ForOrder(Phase.Last).Subscribe(next => calls.Enqueue($"new:{next}"));
                stream.ForOrderAsync(Phase.Last).Subscribe(_ => calls.Enqueue("cancelled")).Dispose();
            });
            removed = stream.ForOrder(Phase.Last).Subscribe(_ => calls.Enqueue("removed"));

            stream.OnNext(1);
            Assert.That(calls, Is.Empty);
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(new[] { "new:2" }));
            pending.Dispose();
            stream.OnNext(3);
            Assert.That(calls, Has.Count.EqualTo(1));
        }

        [Test]
        public void PendingSubscriptionCanBeRemovedFromALaterWorkerInvocation()
        {
            using var stream = new EventStream<int>();
            IDisposable pending = null;
            var calls = 0;
            stream.ForOrderAsync(Phase.First).Subscribe(value =>
            {
                if (value == 1) pending = stream.ForOrder(Phase.Last).Subscribe(_ => calls++);
                else pending.Dispose();
            });

            stream.OnNext(1);
            stream.OnNext(2);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void EachWorkerPreservesItsOwnMutationOrder()
        {
            using var stream = new EventStream<int>();
            using var other = new EventStream<int>();
            using var secondFinished = new ManualResetEventSlim();
            stream.ForOrderAsync(Phase.First).Subscribe(_ =>
            {
                Assert.That(secondFinished.Wait(Timeout), Is.True);
                stream.Enabled = false;
                stream.Enabled = true;
            });
            stream.ForOrderAsync(Phase.First).Subscribe(_ =>
            {
                other.Enabled = false;
                secondFinished.Set();
            });

            stream.OnNext(1);

            Assert.That(stream.Enabled, Is.True);
            Assert.That(other.Enabled, Is.False);
        }

        [TestCase("dispose")]
        [TestCase("complete")]
        [TestCase("error")]
        public void TerminationWaitsForTheBatchAndStopsLaterGroups(string operation)
        {
            using var stream = new EventStream<int>();
            var calls = 0;
            var later = 0;
            var notifications = 0;
            var publisher = Thread.CurrentThread.ManagedThreadId;
            var error = new Exception("terminal");
            stream.ForOrderAsync(Phase.First).Subscribe(_ =>
            {
                Interlocked.Increment(ref calls);
                if (operation == "dispose") stream.Dispose();
                else if (operation == "complete") stream.OnCompleted();
                else stream.OnError(error);

                Assert.That(stream.IsDisposed, Is.False);
            });
            stream.ForOrderAsync(Phase.First).Subscribe(_ => Interlocked.Increment(ref calls), () =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(publisher));
                Assert.That(calls, Is.EqualTo(2));
                notifications++;
            }, exception =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(publisher));
                Assert.That(exception, Is.SameAs(error));
                Assert.That(calls, Is.EqualTo(2));
                notifications++;
            });
            stream.ForOrder(Phase.Last).Subscribe(_ => later++);

            stream.OnNext(1);

            Assert.That(calls, Is.EqualTo(2));
            Assert.That(later, Is.Zero);
            Assert.That(notifications, Is.EqualTo(operation == "dispose" ? 0 : 1));
            Assert.That(stream.IsDisposed, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailuresAreReportedOnThePublisherAfterAllWorkersFinish(bool handled)
        {
            using var stream = new EventStream<int>();
            using var secondFinished = new ManualResetEventSlim();
            var first = new Exception("first");
            var second = new Exception("second");
            var reports = new List<Exception>();
            var publisher = Thread.CurrentThread.ManagedThreadId;
            var later = 0;
            ExceptionHandler.SetHandler(error =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(publisher));
                reports.Add(error);
                if (!handled) throw error;
            });
            stream.ForOrderAsync(Phase.First).Subscribe(_ =>
            {
                Assert.That(secondFinished.Wait(Timeout), Is.True);
                throw first;
            });
            stream.ForOrderAsync(Phase.First).Subscribe(_ =>
            {
                secondFinished.Set();
                throw second;
            });
            stream.ForOrder(Phase.Last).Subscribe(_ => later++);

            if (handled) stream.OnNext(1);
            else Assert.That(Assert.Throws<Exception>(() => stream.OnNext(1)), Is.AnyOf(first, second));

            Assert.That(reports, Is.EquivalentTo(new[] { first, second }));
            Assert.That(later, Is.EqualTo(handled ? 1 : 0));
            stream.OnNext(2);
            Assert.That(reports, Has.Count.EqualTo(2));
            Assert.That(later, Is.EqualTo(handled ? 2 : 1));
        }

        [Test]
        public void PendingDisposalRejectsRegistrationWithoutLosingOtherStreamChanges()
        {
            using var stream = new EventStream<int>();
            using var target = new EventStream<string>();
            var reports = new List<Exception>();
            ExceptionHandler.SetHandler(reports.Add);
            var workers = 0;
            stream.ForOrderAsync(Phase.First).Subscribe(_ =>
            {
                stream.Dispose();
                Assert.Throws<ObjectDisposedException>(() => stream.Subscribe(_ => { }));
                target.Enabled = false;
            });
            stream.ForOrderAsync(Phase.First).Subscribe(_ => Interlocked.Increment(ref workers));

            stream.OnNext(1);

            Assert.That(workers, Is.EqualTo(1));
            Assert.That(reports, Is.Empty);
            Assert.That(target.Enabled, Is.False);
            Assert.That(stream.IsDisposed, Is.True);
        }

        [Test]
        public void DeferredBranchCreationDoesNotConnectACancelledSubscription()
        {
            using var stream = new EventStream<int>();
            EventStream<int> branch = null;
            var calls = 0;
            stream.ForOrderAsync(Phase.First).Subscribe(value =>
            {
                if (value != 1) return;

                branch = stream.Branch();
                branch.Subscribe(_ => calls++).Dispose();
            });

            stream.OnNext(1);
            Assert.That(branch.IsDisposed, Is.False);
            using var live = branch.Subscribe(_ => calls++);
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(1));
            branch.Dispose();
        }

        [Test]
        public void ExplicitPublicationCompletesItsOwnLifecycleAndRestoresTheOuterWorkerScope()
        {
            using var source = new EventStream<int>();
            using var target = new EventStream<int>();
            using var taken = target.Take(1);
            var calls = 0;
            taken.ForOrderAsync(Phase.First).Subscribe(_ => Interlocked.Increment(ref calls));
            source.ForOrderAsync(Phase.First).Subscribe(_ =>
            {
                target.OnNext(7);
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(taken.IsDisposed, Is.True);
                source.Dispose();
                Assert.That(source.IsDisposed, Is.False);
            });

            source.OnNext(1);
            Assert.That(source.IsDisposed, Is.True);
        }

        [Test]
        public void SameStreamReentryStopsLaterGroupsAndAllowsTheNextValue()
        {
            using var stream = new EventStream<int>();
            var calls = 0;
            stream.ForOrderAsync(Phase.First).Subscribe(_ => stream.OnNext(2));
            stream.ForOrder(Phase.Last).Subscribe(_ => calls++);

            Assert.Throws<InvalidOperationException>(() => stream.OnNext(1));
            Assert.That(calls, Is.Zero);
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void RemovingABoundaryAtTheJoinSkipsTheDetachedStream()
        {
            using var stream = new EventStream<int>();
            var path = stream.ForOrderAsync(Phase.First);
            var calls = new ConcurrentQueue<string>();
            using var branch = path.Branch();
            path.Subscribe(_ => branch.Dispose());
            branch.Subscribe(_ => calls.Enqueue("branch"));
            path.Where(_ => true).Subscribe(_ => calls.Enqueue("tail"));

            stream.OnNext(1);

            Assert.That(calls, Is.EqualTo(new[] { "tail" }));
        }

        [Test]
        public void MergedBranchesFinishOneInputBeforeStartingTheNext()
        {
            using var stream = new EventStream<int>();
            var path = stream.ForOrderAsync(Phase.First);
            using var left = path.Branch();
            using var right = path.Branch();
            using var merged = left.Merge(right);
            var active = 0;
            var calls = 0;
            merged.ForOrderAsync(Phase.First).Subscribe(_ =>
            {
                Assert.That(Interlocked.Increment(ref active), Is.EqualTo(1));
                Interlocked.Increment(ref calls);
                Interlocked.Decrement(ref active);
            });

            stream.OnNext(1);

            Assert.That(calls, Is.EqualTo(2));
            Assert.That(active, Is.Zero);
        }

        [Test]
        public void UnhandledFilterFailureDiscardsUnstartedWorkers()
        {
            using var stream = new EventStream<int>();
            var error = new Exception("predicate");
            var calls = new ConcurrentQueue<string>();
            var path = stream.ForOrderAsync(Phase.First);
            path.Subscribe(_ => calls.Enqueue("before"));
            var failed = (Handle)path.Where(_ => throw error).Subscribe(_ => calls.Enqueue("unreachable"));
            path.Where(_ => true).Subscribe(_ => calls.Enqueue("after"));

            Assert.That(Assert.Throws<Exception>(() => stream.OnNext(1)), Is.SameAs(error));

            Assert.That(failed.IsDisposed, Is.True);
            Assert.That(calls, Is.Empty);
            stream.OnNext(2);
            Assert.That(calls, Is.EquivalentTo(new[] { "before", "after" }));
        }

        [Test]
        public void SameBatchRemovalDoesNotCancelAnotherWorkerButSkipsItsNextValue()
        {
            using var stream = new EventStream<int>();
            using var removed = new ManualResetEventSlim();
            var calls = 0;
            IDisposable other = null;
            var path = stream.ForOrderAsync(Phase.First);
            path.Subscribe(_ =>
            {
                other.Dispose();
                removed.Set();
            });
            other = path.Subscribe(_ =>
            {
                Assert.That(removed.Wait(Timeout), Is.True);
                Interlocked.Increment(ref calls);
            });

            stream.OnNext(1);
            stream.OnNext(2);

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void OrdersAcceptIntExtremesAndKeepTheExistingDepthLimit()
        {
            using var stream = new EventStream<int>();
            var calls = new ConcurrentQueue<int>();
            stream.ForOrderAsync((Phase)int.MaxValue).Subscribe(_ => calls.Enqueue(2));
            stream.ForOrder((Phase)int.MinValue).Subscribe(_ => calls.Enqueue(1));
            stream.ForOrderAsync((Phase)int.MinValue).Subscribe(_ => calls.Enqueue(0));
            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.Throws<OverflowException>(() => stream.ForOrderAsync(WideOrder.TooLarge));

            var path = stream.ForOrderAsync(Phase.First);
            for (int i = 1; i < 256; i++) path = path.ForOrderAsync(Phase.First);
            Assert.Throws<InvalidOperationException>(() => path.ForOrderAsync(Phase.First));
        }
    }
}
