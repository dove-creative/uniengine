using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;
using Handle = BlackThunder.UniEngine.Core.Handle;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamMainTests
    {
        private enum Phase { Update, Render }


        // Content --------------------
        // Fixture lifecycle
        [OneTimeSetUp]
        public void CheckInitialMain()
        {
            Assert.That(EventStream.Main, Is.Not.Null);
            Assert.That(EventStream.Main.Enabled, Is.True);
            Assert.That(EventStream.Main.IsDisposed, Is.False);
        }

        [SetUp]
        public void SetUp() => EventStream.ResetMain();

        [TearDown]
        public void TearDown() => EventStream.ResetMain();

        // Publication
        [Test]
        public void MainRemainsStableAndForwardsElapsedSeconds()
        {
            var main = EventStream.Main;
            var values = new List<float>();
            using var subscription = main.Subscribe(values.Add);

            EventStream.Main.OnNext(0.016f);
            EventStream.Main.OnNext(0f);
            EventStream.Main.OnNext(0.025f);

            Assert.That(EventStream.Main, Is.SameAs(main));
            Assert.That(values, Is.EqualTo(new[] { 0.016f, 0f, 0.025f }));
        }

        [Test]
        public void MainCombinesFiltersWithEnumOrderGroups()
        {
            var calls = new List<(Phase Phase, float DeltaTime)>();
            var positive = EventStream.Main.Where(deltaTime => deltaTime > 0f);
            using var render = positive.ForOrder(Phase.Render).Subscribe(value => calls.Add((Phase.Render, value)));
            using var update = positive.ForOrder(Phase.Update).Subscribe(value => calls.Add((Phase.Update, value)));

            EventStream.Main.OnNext(0f);
            EventStream.Main.OnNext(0.016f);

            Assert.That(calls, Is.EqualTo(new[] { (Phase.Update, 0.016f), (Phase.Render, 0.016f) }));
        }

        // Reset and ownership
        [Test]
        public void ResetMainReleasesOldPathsWithoutNotificationsOrTransferringSubscriptions()
        {
            var previous = EventStream.Main;
            var path = previous.Where(value => value > 0f);
            using var branch = path.Branch();
            var previousValues = new List<float>();
            var branchValues = new List<float>();
            var notifications = 0;
            var rootHandle = (Handle)previous.Subscribe(
                previousValues.Add, () => notifications++, _ => notifications++);
            var branchHandle = (Handle)branch.Subscribe(
                branchValues.Add, () => notifications++, _ => notifications++);
            previous.OnNext(0.016f);
            previous.Enabled = false;

            EventStream.ResetMain();

            var current = EventStream.Main;
            Assert.That(current, Is.Not.SameAs(previous));
            Assert.That(current.Enabled, Is.True);
            Assert.That(current.IsDisposed, Is.False);
            Assert.That(previous.IsDisposed && branch.IsDisposed, Is.True);
            Assert.That(rootHandle.IsDisposed && branchHandle.IsDisposed, Is.True);
            Assert.That(notifications, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => previous.OnNext(0.02f));
            Assert.Throws<ObjectDisposedException>(() => path.Subscribe(_ => { }));

            current.OnNext(0.025f);
            var currentValues = new List<float>();
            using var currentHandle = current.Subscribe(currentValues.Add);
            current.OnNext(0.03f);
            rootHandle.Dispose();
            branchHandle.Dispose();

            Assert.That(previousValues, Is.EqualTo(new[] { 0.016f }));
            Assert.That(branchValues, Is.EqualTo(new[] { 0.016f }));
            Assert.That(currentValues, Is.EqualTo(new[] { 0.03f }));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void ResetMainDoesNotAffectIndependentStreams()
        {
            using var independent = new EventStream<float>();
            var values = new List<float>();
            using var subscription = independent.Subscribe(values.Add);
            independent.OnNext(0.01f);

            EventStream.ResetMain();
            EventStream.Main.OnNext(0.02f);
            independent.OnNext(0.03f);

            Assert.That(independent.IsDisposed, Is.False);
            Assert.That(values, Is.EqualTo(new[] { 0.01f, 0.03f }));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ResetMainReplacesTerminatedStreamsAndCanBeRepeated(bool completed, bool errored)
        {
            var previous = EventStream.Main;
            if (completed) previous.OnCompleted();
            else if (errored) previous.OnError(new ApplicationException("host failure"));
            else previous.Dispose();

            Assert.That(EventStream.Main, Is.SameAs(previous));
            Assert.That(previous.IsDisposed, Is.True);

            for (int i = 0; i < 3; i++)
            {
                EventStream.ResetMain();
                var current = EventStream.Main;
                Assert.That(current, Is.Not.SameAs(previous));
                Assert.That(previous.IsDisposed, Is.True);
                Assert.That(current.Enabled, Is.True);
                Assert.That(current.IsDisposed, Is.False);

                var values = new List<float>();
                using var subscription = current.Subscribe(values.Add);
                current.OnNext(0.02f);
                Assert.That(values, Is.EqualTo(new[] { 0.02f }));
                previous = current;
            }
        }

        // Dispatch boundaries
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ResetMainRejectsSynchronousAndParallelCallbacks(bool independent, bool parallel)
        {
            var main = EventStream.Main;
            using var source = independent ? new EventStream<float>() : main;
            var path = parallel ? source.ForOrderAsync(Phase.Update) : source.ForOrder(Phase.Update);
            Exception failure = null;
            var attempts = 0;
            var laterCalls = 0;
            using var subscription = path.Subscribe(_ =>
            {
                attempts++;
                try { EventStream.ResetMain(); }
                catch (Exception exception) { failure = exception; }
            });
            using var later = source.ForOrder(Phase.Render).Subscribe(_ => laterCalls++);

            source.OnNext(0.016f);

            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            Assert.That(EventStream.Main, Is.SameAs(main));
            Assert.That(main.IsDisposed, Is.False);
            Assert.That(laterCalls, Is.EqualTo(1));

            EventStream.ResetMain();
            Assert.That(main.IsDisposed, Is.True);
            Assert.That(EventStream.Main, Is.Not.SameAs(main));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ResetMainRejectsCompletionAndErrorCallbacks(bool independent, bool errored)
        {
            var main = EventStream.Main;
            using var source = independent ? new EventStream<float>() : main;
            var attempts = 0;
            Exception failure = null;
            Exception receivedError = null;
            var hostFailure = new ApplicationException("host failure");

            void TryReset()
            {
                attempts++;
                try { EventStream.ResetMain(); }
                catch (Exception exception) { failure = exception; }
            }

            using var subscription = source.Subscribe(_ => { }, TryReset, exception =>
            {
                receivedError = exception;
                TryReset();
            });

            if (errored) source.OnError(hostFailure);
            else source.OnCompleted();

            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            Assert.That(receivedError, Is.SameAs(errored ? hostFailure : null));
            Assert.That(EventStream.Main, Is.SameAs(main));
            Assert.That(main.IsDisposed, Is.EqualTo(!independent));

            EventStream.ResetMain();
            Assert.That(EventStream.Main, Is.Not.SameAs(main));
            Assert.That(EventStream.Main.Enabled, Is.True);
        }
    }
}
