using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class HandleTokenIntegrationTests
    {
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // I1: The returned handle releases only its own stream subscription.
        [Test]
        public void DisposingTheReturnedHandleStopsOnlyItsSubscription()
        {
            using var stream = new EventStream<int>();
            var first = new List<int>();
            var second = new List<int>();
            using var handle = (Handle)stream.Subscribe(first.Add);
            using var other = stream.Subscribe(second.Add);
            stream.OnNext(1);
            handle.Dispose();
            handle.Dispose();
            stream.OnNext(2);
            Assert.That(handle.IsDisposed, Is.True);
            Assert.That(first, Is.EqualTo(new[] { 1 }));
            Assert.That(second, Is.EqualTo(new[] { 1, 2 }));
        }

        // I2: One token can wake both owners, including when one wake callback reports a failure.
        [TestCase(false)]
        [TestCase(true)]
        public void SharedTokenWakesBothHubsOnce(bool failFirstWake)
        {
            using var first = new EventHub<int>(new object());
            using var second = new EventHub<int>(new object());
            using var token = new Token();
            var failure = new ApplicationException("wake");
            var reports = new List<Exception>();
            var wakes = new int[2];
            var updates = new List<int>();
            ExceptionHandler.SetHandler(reports.Add);
            first.Updated += value => updates.Add(value);
            second.Updated += value => updates.Add(value * 10);
            first.Sleep(token, () =>
            {
                wakes[0]++;
                Assert.That(first.IsAwake, Is.True);
                if (failFirstWake) throw failure;
            });
            second.Sleep(token, () => wakes[1]++);
            first.Update(1);
            second.Update(1);
            Assert.That(updates, Is.Empty);
            Assert.That(first.IsAwake, Is.False);
            Assert.That(second.IsAwake, Is.False);
            token.Dispose();
            token.Dispose();
            Assert.That(first.IsAwake, Is.True);
            Assert.That(second.IsAwake, Is.True);
            Assert.That(wakes, Is.EqualTo(new[] { 1, 1 }));
            Assert.That(reports, Is.EqualTo(failFirstWake ? new[] { failure } : Array.Empty<Exception>()));
            first.Update(2);
            second.Update(2);
            Assert.That(updates, Is.EqualTo(new[] { 2, 20 }));
        }
    }
}
