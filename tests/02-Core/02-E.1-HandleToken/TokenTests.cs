using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class TokenTests
    {
        private class CallbackTarget
        {
            public int Calls { get; private set; }
            public void Invoke() => Calls++;
        }


        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // T1: Both empty construction paths allow later registration.
        [TestCase(false)]
        [TestCase(true)]
        public void EmptyConstructorsAcceptLaterCallbacks(bool nullAction)
        {
            var token = nullAction ? new Token(null) : new Token();
            var calls = 0;
            token.Action += () => calls++;
            Assert.That(token.IsDisposed, Is.False);
            Assert.That(calls, Is.Zero);
            token.Dispose();
            token.Dispose();
            Assert.That(calls, Is.EqualTo(1));
        }

        // T1-T2: Removing a duplicate affects the last occurrence, including constructor callbacks.
        [Test]
        public void SingleRemovalRemovesTheLastOccurrence()
        {
            var calls = new List<int>();
            Action first = () => calls.Add(1);
            Action second = () => calls.Add(2);
            var token = new Token(first);
            token.Action += second;
            token.Action += first;
            token.Action -= first;
            Assert.That(calls, Is.Empty);
            token.Dispose();
            Assert.That(calls, Is.EqualTo(new[] { 1, 2 }));
        }

        // T2: Multicast removal requires a contiguous match and removes the last matching group.
        [TestCase(false)]
        [TestCase(true)]
        public void MulticastRemovalUsesTheLastContiguousMatch(bool contiguous)
        {
            var calls = new List<int>();
            Action first = () => calls.Add(1);
            Action second = () => calls.Add(2);
            Action third = () => calls.Add(3);
            var token = new Token(contiguous ? first + second + third + first + second : first + third + second);
            token.Action -= first + second;
            token.Dispose();
            Assert.That(calls, Is.EqualTo(contiguous ? new[] { 1, 2, 3 } : new[] { 1, 3, 2 }));
        }

        // T3: Null changes and unknown removals preserve the registered action.
        [Test]
        public void NullChangesAndUnknownRemovalAreIgnored()
        {
            var calls = 0;
            var token = new Token(() => calls++);
            token.Action += null;
            token.Action -= null;
            token.Action -= () => Assert.Fail("Unknown callback");
            token.Dispose();
            Assert.That(calls, Is.EqualTo(1));
        }

        // T4: Disposal freezes additions and removals before the first callback is invoked.
        [Test]
        public void DisposalFreezesTheSnapshotAndRejectsFurtherAdditions()
        {
            var calls = new List<int>();
            Action last = () => calls.Add(2);
            Token token = null;
            token = new Token(() =>
            {
                calls.Add(1);
                Assert.That(token.IsDisposed, Is.True);
                Assert.Throws<ObjectDisposedException>(() => token.Action += last);
                Assert.Throws<ObjectDisposedException>(() => token.Action += null);
                token.Action -= last;
                token.Action -= null;
                token.Dispose();
            });

            token.Action += last;
            token.Dispose();
            Assert.Throws<ObjectDisposedException>(() => token.Action += last);
            Assert.Throws<ObjectDisposedException>(() => token.Action += null);
            token.Action -= last;
            token.Dispose();
            Assert.That(calls, Is.EqualTo(new[] { 1, 2 }));
        }

        // T5: An active token must not keep an unsubscribed callback or its target alive.
        [Test]
        public void UnsubscribedCallbacksCanBeCollectedBeforeDisposal()
        {
            using var token = new Token();
            var (callback, target) = RegisterAndRemove(token);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.That(callback.IsAlive, Is.False);
            Assert.That(target.IsAlive, Is.False);
            GC.KeepAlive(token);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference Callback, WeakReference Target) RegisterAndRemove(Token token)
        {
            var target = new CallbackTarget();
            Action callback = target.Invoke;
            token.Action += callback;
            token.Action -= callback;
            return (new WeakReference(callback), new WeakReference(target));
        }
    }
}
