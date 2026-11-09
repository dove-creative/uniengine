using System;
using System.Collections.Generic;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;
using Handle = Blackthunder.UniEngine.Core.Handle;

namespace BlackThunder.UniEngine.Tests
{
    public class EventStreamLifecycleTests
    {
        // Content --------------------
        // Source ownership and termination
        [TestCase(false)]
        [TestCase(true)]
        public void DisabledStreamSkipsPredicatesAndNotifiesEverySubscriberInDepthFirstOrder(bool error)
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var exceptions = new List<Exception>();
            var failure = new ApplicationException("source");
            var predicateCalls = 0;
            var left = stream.Where(_ => { predicateCalls++; return false; });
            var nested = left.Where(_ => { predicateCalls++; return false; });
            var right = stream.Where(_ => { predicateCalls++; return false; });

            Observer Receiver(string name, Action terminating = null)
            {
                void Record(string signal)
                {
                    calls.Add(name + signal);
                    terminating?.Invoke();
                }

                return new Observer(
                    _ => calls.Add("value"),
                    () => Record("-completed"),
                    exception => { Record("-error"); exceptions.Add(exception); });
            }

            var leaf = (Handle)nested.Subscribe(Receiver("leaf"));
            var parent = (Handle)left.Subscribe(Receiver("left"));
            var root = (Handle)stream.Subscribe(Receiver("root"));
            var earlierHandlesActive = false;
            var sibling = (Handle)right.Subscribe(
                Receiver(
                    "right",
                    () => earlierHandlesActive = !root.IsDisposed && !parent.IsDisposed && !leaf.IsDisposed));

            stream.Enabled = false;
            stream.OnNext(7);
            Assert.That(predicateCalls, Is.Zero);
            Assert.That(calls, Is.Empty);

            if (error) stream.OnError(failure);
            else stream.OnCompleted();

            var suffix = error ? "-error" : "-completed";
            Assert.That(
                calls,
                Is.EqualTo(new[] { "root" + suffix, "left" + suffix, "leaf" + suffix, "right" + suffix }));
            Assert.That(
                exceptions,
                Is.EqualTo(error ? new[] { failure, failure, failure, failure } : Array.Empty<Exception>()));
            Assert.That(predicateCalls, Is.Zero);
            Assert.That(earlierHandlesActive, Is.True, "Notify every subscription before disposing the graph.");
            Assert.That(leaf.IsDisposed && sibling.IsDisposed && parent.IsDisposed && root.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.Zero);
            Assert.That(stream.IsDisposed, Is.True);
        }

        [Test]
        public void ExplicitDisposalSilentlyReleasesTheDefaultSourceAndRejectsDirectPublication()
        {
            using var stream = new EventStream<int>();
            var values = new List<int>();
            var completions = 0;
            var errors = new List<Exception>();
            var handle = (Handle)stream.Take(2).Subscribe(values.Add, () => completions++, errors.Add);
            stream.OnNext(1);
            stream.Dispose();

            Assert.Throws<ObjectDisposedException>(() => stream.OnNext(2));
            Assert.That(values, Is.EqualTo(new[] { 1 }));
            Assert.That(completions, Is.Zero);
            Assert.That(errors, Is.Empty);
            Assert.That(handle.IsDisposed && stream.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.Zero);
        }

        [Test]
        public void ExternalSourceCanPublishAndDisposalOnlyReleasesItsConnection()
        {
            using var source = new ExternalSource();
            using var stream = new EventStream<int>(source);
            var values = new List<int>();
            var terminals = 0;
            var handle = (Handle)stream.Subscribe(values.Add, () => terminals++, _ => terminals++);

            source.Observer.OnNext(7);
            stream.Dispose();
            stream.Dispose();
            Assert.That(values, Is.EqualTo(new[] { 7 }));
            Assert.That(terminals, Is.Zero);
            Assert.That(handle.IsDisposed && stream.IsDisposed, Is.True);
            Assert.That(source.Connection.IsDisposed, Is.True);
            Assert.That(source.IsDisposed, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StreamWithAnExternalSourceAcceptsDirectInputAndControl(bool error)
        {
            using var source = new ExternalSource();
            using var stream = new EventStream<int>(source);
            var values = new List<int>();
            var completions = 0;
            var errors = new List<Exception>();
            var failure = new ApplicationException("direct");
            using var subscription = stream.Subscribe(values.Add, () => completions++, errors.Add);

            stream.OnNext(1);
            stream.Enabled = false;
            stream.OnNext(2);
            source.Observer.OnNext(3);
            stream.Enabled = true;
            source.Observer.OnNext(4);

            if (error) stream.OnError(failure);
            else stream.OnCompleted();

            Assert.That(values, Is.EqualTo(new[] { 1, 4 }));
            Assert.That(completions, Is.EqualTo(error ? 0 : 1));
            Assert.That(errors, Is.EqualTo(error ? new[] { failure } : Array.Empty<Exception>()));
            Assert.That(source.Connection.IsDisposed, Is.True);
            Assert.That(source.IsDisposed, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExternalSourceTerminationReleasesItsConnectionAndForwardsTheOriginalSignal(bool error)
        {
            using var source = new ExternalSource();
            using var stream = new EventStream<int>(source);
            var failure = new ApplicationException("upstream");
            var completed = 0;
            var errors = new List<Exception>();
            var handle = (Handle)stream.Subscribe(_ => { }, () => completed++, errors.Add);

            if (error) source.Observer.OnError(failure);
            else source.Observer.OnCompleted();

            Assert.That(completed, Is.EqualTo(error ? 0 : 1));
            Assert.That(errors, Is.EqualTo(error ? new[] { failure } : Array.Empty<Exception>()));
            Assert.That(handle.IsDisposed && stream.IsDisposed, Is.True);
            Assert.That(source.Connection.IsDisposed, Is.True);
            Assert.That(source.IsDisposed, Is.False);
        }

        [Test]
        public void SynchronouslyCompletedExternalSourceReleasesTheHandleReturnedAfterCompletion()
        {
            using var source = new ExternalSource { CompleteOnSubscribe = true };
            using var stream = new EventStream<int>(source);
            Assert.That(stream.IsDisposed, Is.True);
            Assert.That(source.Connection.IsDisposed, Is.True);
            Assert.That(source.IsDisposed, Is.False);
        }

        // Branch
        [TestCase("Completed")]
        [TestCase("Error")]
        [TestCase("Disposed")]
        public void UnsubscribedBranchDoesNotRegisterAPathAndObservesParentLifetime(string signal)
        {
            using var parent = new EventStream<int>();
            var checks = 0;
            using var branch = parent.Where(_ => { checks++; return true; }).Branch();
            parent.OnNext(1);
            Assert.That(parent.NodeCount, Is.Zero);
            Assert.That(checks, Is.Zero);

            if (signal == "Completed") parent.OnCompleted();
            else if (signal == "Error") parent.OnError(new ApplicationException("parent"));
            else parent.Dispose();

            Assert.That(branch.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => branch.Subscribe(_ => { }));
        }

        [TestCase("Completed")]
        [TestCase("Error")]
        [TestCase("Disposed")]
        public void EndingTheParentForwardsItsSignalAndReleasesTheBranch(string signal)
        {
            using var parent = new EventStream<int>();
            using var branch = parent.Where(_ => false).Branch();
            var values = new List<int>();
            var completions = 0;
            var errors = new List<Exception>();
            var failure = new ApplicationException("parent");
            var handle = (Handle)branch.Take(1).Subscribe(values.Add, () => completions++, errors.Add);
            parent.OnNext(7);

            if (signal == "Completed") parent.OnCompleted();
            else if (signal == "Error") parent.OnError(failure);
            else parent.Dispose();

            Assert.That(values, Is.Empty);
            Assert.That(completions, Is.EqualTo(signal == "Completed" ? 1 : 0));
            Assert.That(errors, Is.EqualTo(signal == "Error" ? new[] { failure } : Array.Empty<Exception>()));
            Assert.That(handle.IsDisposed && branch.IsDisposed && parent.IsDisposed, Is.True);
            Assert.That(branch.NodeCount, Is.Zero);
            Assert.That(parent.NodeCount, Is.Zero);
        }

        [Test]
        public void BranchesPublishAndEnableIndependentlyAndReleaseOnlyTheirOwnPaths()
        {
            using var stream = new EventStream<int>();
            var path = stream.Where(value => value > 0);
            using var left = path.Branch();
            using var right = path.Branch();
            var parentValues = new List<int>();
            var leftValues = new List<int>();
            var rightValues = new List<int>();
            stream.Subscribe(parentValues.Add);
            var leftRoot = left.Subscribe(leftValues.Add);
            var leftLeaf = left.Where(_ => true).Subscribe(_ => { });
            var rightRoot = right.Subscribe(rightValues.Add);
            Assert.That(stream.NodeCount, Is.EqualTo(3));

            stream.OnNext(1);
            left.Enabled = false;
            left.OnNext(10);
            stream.OnNext(2);
            right.OnNext(-3);
            left.Enabled = true;
            left.OnNext(-4);
            stream.OnNext(-5);
            Assert.That(parentValues, Is.EqualTo(new[] { 1, 2, -5 }));
            Assert.That(leftValues, Is.EqualTo(new[] { 1, -4 }));
            Assert.That(rightValues, Is.EqualTo(new[] { 1, 2, -3 }));

            leftRoot.Dispose();
            Assert.That(left.IsDisposed, Is.False);
            leftLeaf.Dispose();
            Assert.That(left.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.EqualTo(2));
            stream.OnNext(6);
            Assert.That(rightValues, Is.EqualTo(new[] { 1, 2, -3, 6 }));
            rightRoot.Dispose();
            Assert.That(right.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.Zero);
            Assert.That(stream.IsDisposed, Is.False);
        }

        // External sources
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void ExternalSourceCanFinishDuringConnectionAndReleasesTheReturnedHandleOnce(
            bool error,
            bool completeTwice)
        {
            var failure = new ApplicationException("upstream");

            var source = new SubscribingSource(observer =>
            {
                if (error) observer.OnError(failure);
                else observer.OnCompleted();

                if (completeTwice) observer.OnCompleted();
            });

            using var stream = new EventStream<int>(source);

            Assert.That(stream.IsDisposed, Is.True);
            Assert.That(source.Releases, Is.EqualTo(1));
        }

        [Test]
        public void ExternalSubscribeFailureDisposesTheStreamAndKeepsTheOriginalException()
        {
            var failure = new ApplicationException("connection");
            var source = new SubscribingSource(_ => throw failure);

            var thrown = Assert.Throws<ApplicationException>(() => new EventStream<int>(source));

            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(source.Releases, Is.Zero);
        }

        // Merge
        [Test]
        public void MergedStreamDeliversBothInputsAndCompletesWhenAllUpstreamsComplete()
        {
            using var left = new EventStream<int>();
            using var right = new EventStream<int>();
            using var merged = left.Merge(right);
            var values = new List<int>();
            var completions = 0;
            merged.Subscribe(values.Add, () => completions++, _ => Assert.Fail("Unexpected source error."));
            left.OnNext(1);
            right.OnNext(2);
            left.OnCompleted();
            Assert.That(merged.IsDisposed, Is.False);
            right.OnNext(3);
            right.OnCompleted();
            Assert.That(values, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(merged.IsDisposed, Is.True);
        }

        [Test]
        public void MergedErrorEndsTheStreamAndReleasesTheOtherInput()
        {
            using var left = new EventStream<int>();
            using var right = new EventStream<int>();
            using var merged = left.Merge(right);
            var failure = new ApplicationException("input");
            var errors = new List<Exception>();
            merged.Subscribe(_ => { }, null, errors.Add);

            left.OnError(failure);

            Assert.That(errors, Is.EqualTo(new[] { failure }));
            Assert.That(merged.IsDisposed, Is.True);
            Assert.That(right.NodeCount, Is.Zero);
            Assert.That(right.IsDisposed, Is.False);
        }

        [Test]
        public void DisposingAMergedStreamSilentlyReleasesItsSubscriptionsAndUpstreamConnections()
        {
            using var upstream = new EventStream<int>();
            using var merged = upstream.Merge();
            var notifications = 0;
            var handle = (Handle)merged.Take(1)
                .Subscribe(_ => notifications++, () => notifications++, _ => notifications++);
            merged.Dispose();

            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(merged.NodeCount, Is.Zero);
            Assert.That(notifications, Is.Zero);
            Assert.That(upstream.NodeCount, Is.Zero);
            Assert.That(upstream.IsDisposed, Is.False);
        }

        // Helpers
        private class SubscribingSource : IObservable<int>
        {
            public int Releases { get; private set; }
            private readonly Action<IObserver<int>> _onSubscribe;

            public SubscribingSource(Action<IObserver<int>> onSubscribe) => _onSubscribe = onSubscribe;

            public IDisposable Subscribe(IObserver<int> observer)
            {
                _onSubscribe(observer);
                return new Connection(this);
            }

            private class Connection : IDisposable
            {
                private readonly SubscribingSource _source;
                public Connection(SubscribingSource source) => _source = source;
                public void Dispose() => _source.Releases++;
            }
        }

        private class ExternalSource : IObservable<int>, IDisposable
        {
            public IObserver<int> Observer { get; private set; }
            public Handle Connection { get; } = new(() => { });
            public bool CompleteOnSubscribe { get; set; }
            public bool IsDisposed { get; private set; }

            public IDisposable Subscribe(IObserver<int> observer)
            {
                Observer = observer;

                if (CompleteOnSubscribe) observer.OnCompleted();

                return Connection;
            }

            public void Dispose() => IsDisposed = true;
        }

        private class Observer : IObserver<int>
        {
            private readonly Action<int> _updated;
            private readonly Action _completed;
            private readonly Action<Exception> _errored;

            public Observer(Action<int> updated, Action completed, Action<Exception> errored)
            {
                _updated = updated;
                _completed = completed;
                _errored = errored;
            }

            public void OnNext(int value) => _updated(value);
            public void OnCompleted() => _completed();
            public void OnError(Exception exception) => _errored(exception);
        }
    }
}
