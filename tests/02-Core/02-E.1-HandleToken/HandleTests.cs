using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class HandleTests
    {
        // Internal
        private class GuardedHandle : Handle
        {
            public void Use() => ThrowIfDisposed();
        }

        private class CallbackTarget
        {
            public bool Fail { get; set; }
            public void Invoke()
            {
                if (Fail) throw new ApplicationException("callback");
            }
        }


        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // H1: A null callback is a valid empty lifetime.
        [Test]
        public void NullActionCanBeDisposedRepeatedly()
        {
            var handle = new Handle(null);
            Assert.That(handle.IsDisposed, Is.False);
            handle.Dispose();
            handle.Dispose();
            Assert.That(handle.IsDisposed, Is.True);
        }

        // H1-H2: Construction defers execution and callbacks observe the completed state.
        [Test]
        public void FirstDisposalExecutesSynchronouslyAfterSettingState()
        {
            Handle handle = null;
            var calls = 0;
            handle = new Handle(() =>
            {
                Assert.That(handle.IsDisposed, Is.True);
                calls++;
            });

            handle.Description = "release subscription";
            Assert.That(handle.Description, Is.EqualTo("release subscription"));
            Assert.That(handle.IsDisposed, Is.False);
            Assert.That(calls, Is.Zero);
            handle.Dispose();
            Assert.That(calls, Is.EqualTo(1));
            handle.Dispose();
            Assert.That(calls, Is.EqualTo(1));
        }

        // H2: Each multicast occurrence is retained in registration order.
        [Test]
        public void MulticastKeepsOrderAndDuplicateOccurrences()
        {
            var calls = new List<int>();
            Action first = () => calls.Add(1);
            Action second = () => calls.Add(2);
            var handle = new Handle(first + second + first);
            handle.Dispose();
            handle.Dispose();
            Assert.That(calls, Is.EqualTo(new[] { 1, 2, 1 }));
        }

        // H3: Reentrant disposal cannot restart the current snapshot.
        [Test]
        public void ReentrantDisposalKeepsEachOccurrenceSingle()
        {
            var calls = new List<int>();
            Handle handle = null;
            Action first = () =>
            {
                calls.Add(1);
                handle.Dispose();
            };

            handle = new Handle(first + (() => calls.Add(2)));
            handle.Dispose();
            Assert.That(calls, Is.EqualTo(new[] { 1, 2 }));
        }

        // H4: Both types share continuation, original exception and policy failure contracts.
        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        public void CallbackFailuresFollowTheSelectedPolicy(bool token, int policy)
        {
            var failure = new ApplicationException("callback");
            var policyFailure = new InvalidOperationException("policy");
            var reports = new List<Exception>();
            var calls = new List<int>();
            Action first = () => calls.Add(0);
            Action bad = () => { calls.Add(1); throw failure; };
            Action last = () => calls.Add(2);
            var callbacks = first + bad + last + bad + last;
            Handle handle = token ? new Token(callbacks) : new Handle(callbacks);

            if (policy != 0)
            {
                ExceptionHandler.SetHandler(exception =>
                {
                    reports.Add(exception);
                    if (policy == 2) throw policyFailure;
                });
            }

            if (policy == 0) Assert.That(Assert.Throws<ApplicationException>(handle.Dispose), Is.SameAs(failure));
            else if (policy == 1) handle.Dispose();
            else Assert.That(Assert.Throws<InvalidOperationException>(handle.Dispose), Is.SameAs(policyFailure));

            Assert.That(handle.IsDisposed, Is.True);
            handle.Dispose();
            Assert.That(calls, Is.EqualTo(policy == 1 ? new[] { 0, 1, 2, 1, 2 } : new[] { 0, 1 }));
            var expectedReports = policy == 0 ? Array.Empty<Exception>()
                : policy == 1 ? new[] { failure, failure } : new[] { failure };
            Assert.That(reports, Is.EqualTo(expectedReports));
        }

        // H5: A live disposed object must release both the callback and its target, even after failure.
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void DisposedHandlesReleaseCallbackReferences(bool token, bool fail)
        {
            var (handle, callback, target) = CreateDisposedHandle(token, fail);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.That(callback.IsAlive, Is.False);
            Assert.That(target.IsAlive, Is.False);
            GC.KeepAlive(handle);
        }

        // H6: Derived types can use the protected lifetime guard.
        [Test]
        public void DerivedGuardRejectsUseAfterDisposal()
        {
            var handle = new GuardedHandle();
            Assert.DoesNotThrow(handle.Use);
            handle.Dispose();
            Assert.Throws<ObjectDisposedException>(handle.Use);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (Handle Handle, WeakReference Callback, WeakReference Target) CreateDisposedHandle(
            bool token,
            bool fail)
        {
            var target = new CallbackTarget { Fail = fail };
            Action callback = target.Invoke;
            Handle handle = token ? new Token(callback) : new Handle(callback);
            if (fail) Assert.Throws<ApplicationException>(handle.Dispose);
            else handle.Dispose();
            return (handle, new WeakReference(callback), new WeakReference(target));
        }
    }
}
