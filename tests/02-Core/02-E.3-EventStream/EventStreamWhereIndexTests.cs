using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamWhereIndexTests
    {
        // Content --------------------
        // Sharing across the promotion boundary
        [TestCase(8)]
        [TestCase(16)]
        [TestCase(17)]
        [TestCase(32)]
        [TestCase(33)]
        [TestCase(64)]
        public void WideParentsPreserveMethodAndTargetEquality(int count)
        {
            using var stream = new EventStream<int>();
            Fill(stream, count);
            var probe = new PredicateProbe();
            Func<int, bool> first = probe.Check;
            Func<int, bool> equal = probe.Check;
            Assert.That(equal, Is.Not.SameAs(first));
            var calls = 0;
            stream.Where(first).Subscribe(_ => calls++);
            stream.Where(equal).Subscribe(_ => calls++);
            stream.Where(probe.Other).Subscribe(_ => calls++);
            stream.Where(new PredicateProbe().Check).Subscribe(_ => calls++);

            stream.OnNext(1);

            Assert.That(stream.NodeCount, Is.EqualTo(count + 3));
            Assert.That(probe.Checks, Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(4));
        }

        [Test]
        public void IndexedParentsKeepMulticastOrderAndParentIdentity()
        {
            using var stream = new EventStream<int>();
            var left = stream.Where(value => value > 0);
            var right = stream.Where(value => value < 0);
            Fill(left, 64);
            Fill(right, 64);
            var probe = new PredicateProbe();
            Func<int, bool> combined = probe.Check;
            combined += probe.Other;
            var equal = (Func<int, bool>)combined.Clone();
            Func<int, bool> reversed = probe.Other;
            reversed += probe.Check;
            var leftCalls = 0;
            var rightCalls = 0;
            left.Where(combined).Subscribe(_ => leftCalls++);
            left.Where(equal).Subscribe(_ => leftCalls++);
            left.Where(reversed).Subscribe(_ => leftCalls++);
            right.Where(combined).Subscribe(_ => rightCalls++);

            stream.OnNext(1);
            Assert.That((leftCalls, rightCalls, probe.Checks), Is.EqualTo((3, 0, 4)));
            stream.OnNext(-1);
            Assert.That((leftCalls, rightCalls, probe.Checks), Is.EqualTo((3, 1, 6)));
            Assert.That(stream.NodeCount, Is.EqualTo(133));
        }

        [Test]
        public void PromotionDuringPublicationDefersNewNodesAndSkipsReplacedNodes()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            Func<int, bool> replaced = _ => true;
            var removed = stream.Where(replaced).Subscribe(_ => calls.Add("removed"));
            stream.Subscribe(value =>
            {
                if (value != 1) return;

                Fill(stream, 64);
                removed.Dispose();
                stream.Where(replaced).Subscribe(_ => calls.Add("replacement"));
                stream.Where(replaced).Subscribe(_ => calls.Add("shared"));
            });

            stream.OnNext(1);
            Assert.That(calls, Is.Empty);
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(new[] { "replacement", "shared" }));
            Assert.That(stream.NodeCount, Is.EqualTo(65));
        }

        [Test]
        public void IndexedParentCanShrinkEmptyAndRegisterAgain()
        {
            using var stream = new EventStream<int>();
            var handles = Fill(stream, 64);
            var probe = new PredicateProbe();
            var calls = 0;
            var first = stream.Where(probe.Check).Subscribe(_ => calls++);
            foreach (var handle in handles) handle.Dispose();
            var duplicate = stream.Where(probe.Check).Subscribe(_ => calls++);
            stream.OnNext(1);
            Assert.That((stream.NodeCount, probe.Checks, calls), Is.EqualTo((1, 1, 2)));

            first.Dispose();
            duplicate.Dispose();
            Assert.That(stream.NodeCount, Is.Zero);
            stream.Where(probe.Check).Subscribe(_ => calls++);
            stream.OnNext(2);
            Assert.That((stream.NodeCount, probe.Checks, calls), Is.EqualTo((1, 2, 3)));
        }

        [Test]
        public void FailedIndexedSubtreeCanBeRegisteredAgainWithoutStaleNodes()
        {
            using var stream = new EventStream<int>();
            Fill(stream, 64);
            var reports = new List<Exception>();
            var failure = new ApplicationException("predicate");
            ExceptionHandler.SetHandler(reports.Add);
            try
            {
                Func<int, bool> predicate = value => value == 1 ? throw failure : true;
                var path = stream.Where(predicate);
                Fill(path, 64);
                var calls = 0;
                var failed = (Handle)path.Subscribe(_ => calls++);
                stream.OnNext(1);
                Assert.That(failed.IsDisposed, Is.True);
                Assert.That(reports, Is.EqualTo(new[] { failure }));
                Assert.That(stream.NodeCount, Is.EqualTo(64));

                path.Subscribe(_ => calls++);
                stream.Where(predicate).Subscribe(_ => calls++);
                stream.OnNext(2);
                Assert.That(calls, Is.EqualTo(2));
                Assert.That(stream.NodeCount, Is.EqualTo(65));
            }
            finally
            {
                ExceptionHandler.SetHandler(null);
            }
        }

        [Test]
        public void RemovedIndexedParentsDoNotKeepPredicateTargetsAlive()
        {
            using var stream = new EventStream<int>();
            Fill(stream, 64);
            var target = RegisterAndRemoveSubtree(stream);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.That(target.IsAlive, Is.False);
            Assert.That(stream.NodeCount, Is.EqualTo(64));
            GC.KeepAlive(stream);
        }

        // Helpers
        private static IDisposable[] Fill(EventStream<int> stream, int count) =>
            Fill(condition => stream.Where(condition).Subscribe(_ => { }), count);

        private static IDisposable[] Fill(EventPath<int> path, int count) =>
            Fill(condition => path.Where(condition).Subscribe(_ => { }), count);

        private static IDisposable[] Fill(Func<Func<int, bool>, IDisposable> subscribe, int count)
        {
            var handles = new IDisposable[count];
            for (int i = 0; i < count; i++)
            {
                var target = i + 100;
                handles[i] = subscribe(value => value == target);
            }
            return handles;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RegisterAndRemoveSubtree(EventStream<int> stream)
        {
            var probe = new PredicateProbe();
            var path = stream.Where(probe.Check);
            var handles = Fill(path, 64);
            foreach (var handle in handles) handle.Dispose();
            return new WeakReference(probe);
        }

        private class PredicateProbe
        {
            internal int Checks;

            internal bool Check(int value) { Checks++; return true; }
            internal bool Other(int value) { Checks++; return true; }
        }
    }
}
