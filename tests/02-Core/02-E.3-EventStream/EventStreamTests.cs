using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using Blackthunder.UniEngine.Core.EventStreamCore;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamTests
    {
        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // R2: Removal affects the current publication; new subscriptions wait for the next.
        [Test]
        public void EventStreamAppliesSubscriptionChangesAtTheExpectedTime()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var removed = (IDisposable)null;
            stream.Subscribe(value =>
            {
                calls.Add($"first:{value}");
                if (value != 1) return;
                removed.Dispose();
                stream.Subscribe(next => calls.Add($"late:{next}"));
            });
            removed = stream.Subscribe(value => calls.Add($"removed:{value}"));
            stream.OnNext(1);
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(new[] { "first:1", "first:2", "late:2" }));
        }

        // R2: Fault isolation or propagation still permits a later publication.
        [TestCase(false)]
        [TestCase(true)]
        public void EventStreamRecoversAfterCallbackFailure(bool handled)
        {
            using var stream = new EventStream<int>();
            var calls = new List<int>();
            var failure = new ApplicationException("subscriber");
            var reported = new List<Exception>();
            if (handled) ExceptionHandler.SetHandler(reported.Add);
            stream.Subscribe(_ => throw failure);
            stream.Subscribe(calls.Add);
            if (handled)
            {
                stream.OnNext(1);
                Assert.That(reported, Is.EqualTo(new[] { failure }));
            }
            else
            {
                Assert.That(Assert.Throws<ApplicationException>(() => stream.OnNext(1)), Is.SameAs(failure));
            }
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(handled ? new[] { 1, 2 } : new[] { 2 }));
        }

        // R2: Disposal inside a callback stops the remaining receivers and cleans up.
        [Test]
        public void EventStreamDisposalDuringPublicationStopsRemainingCallbacks()
        {
            using var stream = new EventStream<int>();
            var laterCalls = 0;
            stream.Subscribe(_ => stream.Dispose());
            stream.Subscribe(_ => laterCalls++);
            stream.OnNext(1);
            Assert.That(laterCalls, Is.Zero);
            Assert.That(stream.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => stream.OnNext(2));
        }

        // Skip stops counting once its prefix is consumed, so the counter never wraps around.
        [TestCase(2, new[] { 3, 4 })]
        [TestCase(0, new[] { 1, 2, 3, 4 })]
        [TestCase(int.MinValue, new[] { 1, 2, 3, 4 })]
        public void SkipForwardsEveryValueAfterTheSkippedPrefix(int count, int[] expected)
        {
            using var source = new EventStream<int>();
            using var skipped = source.Skip(count);
            var values = new List<int>();
            skipped.Subscribe(values.Add);
            for (var value = 1; value <= 4; value++) source.OnNext(value);
            Assert.That(values, Is.EqualTo(expected));
        }

        // Every creation path rejects disposed inputs and releases earlier connections.
        [TestCaseSource(nameof(DisposedMergeCases))]
        public void MergeRejectsDisposedInputsAndReleasesConnections(
            Func<IEventStream<int>[], IEventStream<int>> create, bool disposedFirst)
        {
            using var source = new EventStream<int> { TerminationMode = TerminationMode.NoSubscribers };
            using var disposed = new EventStream<int>();
            var values = new List<int>();
            using var subscription = source.Subscribe(values.Add);
            disposed.Dispose();
            var upstreams = disposedFirst
                ? new IEventStream<int>[] { disposed, source }
                : new IEventStream<int>[] { source, disposed };

            Assert.Throws<ObjectDisposedException>(() =>
            {
                using var merged = create(upstreams);
            });

            Assert.That(source.IsDisposed, Is.False);
            source.OnNext(7);
            Assert.That(values, Is.EqualTo(new[] { 7 }));
            subscription.Dispose();
            Assert.That(source.IsDisposed, Is.True, "Failed construction must not leave an upstream subscription.");
        }

        // Valid inputs retain their value and completion behavior; null entries remain optional.
        [TestCase(Merge<int>.CompletionMode.All)]
        [TestCase(Merge<int>.CompletionMode.Any)]
        public void MergePreservesCompletionPolicyForValidInputs(Merge<int>.CompletionMode completionMode)
        {
            using var left = new EventStream<int>();
            using var right = new EventStream<int>();
            using var merged = new Merge<int>(completionMode, left, null, right);
            var values = new List<int>();
            var completions = 0;
            using var subscription = merged.Subscribe(values.Add, () => completions++, null);

            left.OnNext(1);
            right.OnNext(2);
            left.OnCompleted();
            Assert.That(merged.IsDisposed, Is.EqualTo(completionMode == Merge<int>.CompletionMode.Any));

            right.OnNext(3);
            right.OnCompleted();
            Assert.That(values, Is.EqualTo(completionMode == Merge<int>.CompletionMode.All
                ? new[] { 1, 2, 3 } : new[] { 1, 2 }));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(merged.IsDisposed, Is.True);
        }

        private static IEnumerable<TestCaseData> DisposedMergeCases()
        {
            var factories = new (string Name, Func<IEventStream<int>[], IEventStream<int>> Create)[]
            {
                ("ArrayConstructor", upstreams => new Merge<int>(upstreams)),
                ("EnumerableConstructor", upstreams => new Merge<int>((IEnumerable<IEventStream<int>>)upstreams)),
                ("Static", upstreams => EventStreamExtensions.Merge(upstreams)),
                ("StaticWithPolicy", upstreams => EventStreamExtensions.Merge(TerminationMode.Complete, upstreams)),
                ("Extension", upstreams => upstreams[0].Merge(upstreams[1])),
                ("ExtensionWithPolicy", upstreams => upstreams[0].Merge(TerminationMode.Complete, upstreams[1])),
            };

            foreach (var (name, create) in factories)
            {
                yield return new TestCaseData(create, true).SetName($"MergeRejectsDisposedInput_{name}_First");
                yield return new TestCaseData(create, false).SetName($"MergeRejectsDisposedInput_{name}_Last");
            }
        }
    }
}
