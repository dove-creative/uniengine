using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamRowTests
    {
        // Content --------------------
        // Sharing and ordering
        [Test]
        public void WhereRowsShareTheirPositionAndNestedRowsReferenceSeparateNodes()
        {
            using var stream = new EventStream<ulong>();
            var checks = new List<string>();
            var calls = new List<string>();
            Func<ulong, bool> a = _ => { checks.Add("a"); return true; };
            Func<ulong, bool> b = _ => { checks.Add("b"); return true; };
            stream.Where(a).Where(b).Subscribe(_ => calls.Add("nested"));
            stream.Where(a).Subscribe(_ => calls.Add("a1"));
            stream.ForFlags(EventKinds.First).Subscribe(_ => calls.Add("flags"));
            stream.Where(b).Subscribe(_ => calls.Add("b1"));
            stream.Where(a).Subscribe(_ => calls.Add("a2"));

            stream.OnNext(1);

            Assert.That(stream.NodeCount, Is.EqualTo(3));
            Assert.That(checks, Is.EqualTo(new[] { "a", "b", "b" }));
            Assert.That(calls, Is.EqualTo(new[] { "a1", "a2", "nested", "b1", "flags" }));
        }

        [Test]
        public void OverlappingMasksVisitEveryMatchingRowAndItsSubscribersInOrder()
        {
            using var stream = new EventStream<ulong>();
            var calls = new List<string>();
            stream.ForFlags(EventKinds.First).Subscribe(_ => calls.Add("a"));
            stream.ForFlags(EventKinds.Second).Subscribe(_ => calls.Add("b1"));
            stream.ForFlags(EventKinds.Second).Subscribe(_ => calls.Add("b2"));
            stream.ForFlags(EventKinds.Third).Subscribe(_ => calls.Add("c1"));
            stream.ForFlags(EventKinds.Third).Subscribe(_ => calls.Add("c2"));

            stream.OnNext(0);
            Assert.That(calls, Is.Empty);
            stream.OnNext(6);
            Assert.That(calls, Is.EqualTo(new[] { "b1", "b2", "c1", "c2" }));
            Assert.That(stream.NodeCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TerminalSignalsFollowGateNodeAndRowOrderRegardlessOfKeys(bool error)
        {
            using var stream = new EventStream<ulong>();
            using var branch = stream.Branch();
            var calls = new List<string>();
            var failure = new ApplicationException("terminal");
            Action<EventPath<ulong>, string> observe = (path, name) =>
                path.Subscribe(_ => { }, () => calls.Add(name), _ => calls.Add(name));
            observe(stream.Where(_ => true), "where-a");
            observe(stream.ForFlags(EventKinds.First), "flags-a");
            observe(stream.Where(_ => false), "where-b");
            observe(stream.When(99), "when");
            observe(stream.ForOrder((Phase)2), "order-2");
            observe(stream.Debug("terminal"), "debug");
            branch.Subscribe(_ => { }, () => calls.Add("branch"), _ => calls.Add("branch"));
            observe(stream.ForOrder((Phase)1), "order-1");
            observe(stream.ForFlags(EventKinds.Second), "flags-b");
            stream.Subscribe(_ => { }, () => calls.Add("root"), _ => calls.Add("root"));

            if (error) stream.OnError(failure);
            else stream.OnCompleted();

            Assert.That(calls, Is.EqualTo(new[]
            {
                "root", "where-a", "where-b", "flags-a", "flags-b", "when",
                "order-1", "order-2", "debug", "branch"
            }));
            Assert.That(stream.NodeCount, Is.Zero);
            Assert.That(branch.IsDisposed, Is.True);
        }

        // Internal rows and adaptive lookup
        [TestCase("Where")]
        [TestCase("Flags")]
        [TestCase("When")]
        [TestCase("Order")]
        public void KeyedRowsShareConnectionsWithoutBecomingGraphNodes(string kind)
        {
            using var graph = new EventGraph<ulong>(() => { });
            var root = graph.BeginRegistration();
            Func<ulong, bool> predicate = _ => true;
            var first = Find(graph, root, kind, 1, predicate);
            var same = Find(graph, root, kind, 1, predicate);
            var other = Find(graph, root, kind, 2, _ => true);
            var nested = Find(graph, first, kind, 1, predicate);
            graph.Subscribe(first, _ => { }, null, null);
            graph.Subscribe(other, _ => { }, null, null);
            graph.Subscribe(nested, _ => { }, null, null);

            Assert.That(same, Is.SameAs(first));
            Assert.That(other, Is.Not.SameAs(first));
            Assert.That(nested, Is.Not.SameAs(first));
            Assert.That(first.Owner.GetType().Name, Is.EqualTo("Row"));
            Assert.That(first.Owner, Is.Not.InstanceOf<EventGraph<ulong>.NodeChild>());
            Assert.That(first.Owner.GetType().DeclaringType.Name, Is.EqualTo(kind == "Order"
                ? "OrderedNode" : kind + "Node"));
            Assert.That(graph.NodeCount, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdaptiveIndexPromotesAtSeventeenDistinctRowsAndSurvivesShrinkUntilEmpty(bool flags)
        {
            using var graph = new EventGraph<ulong>(() => { });
            var root = graph.BeginRegistration();
            var handles = new List<IDisposable>();
            var predicates = new Func<ulong, bool>[17];
            object node = null;
            for (var index = 0; index < 16; index++)
            {
                var key = (ulong)index + 1;
                predicates[index] = value => value == key;
                var row = Find(graph, root, flags ? "Flags" : "Where", key, predicates[index]);
                node = row.Owner.Node;
                handles.Add(graph.Subscribe(row, _ => { }, null, null));
            }

            Assert.That(ReadField(node, "_index"), Is.Null);
            var duplicate = Find(graph, root, flags ? "Flags" : "Where", 1, predicates[0]);
            using var duplicateHandle = graph.Subscribe(duplicate, _ => { }, null, null);
            Assert.That(ReadField(node, "_index"), Is.Null, "Duplicate subscribers do not promote the row index.");
            predicates[16] = value => value == 17;
            var promoted = Find(graph, root, flags ? "Flags" : "Where", 17, predicates[16]);
            handles.Add(graph.Subscribe(promoted, _ => { }, null, null));
            var indexTable = (IDictionary)ReadField(node, "_index");
            Assert.That(indexTable.Count, Is.EqualTo(17));
            duplicateHandle.Dispose();
            for (var index = 0; index < 16; index++) handles[index].Dispose();

            Assert.That(ReadField(node, "_index"), Is.SameAs(indexTable));
            Assert.That(indexTable.Count, Is.EqualTo(1));
            Assert.That(graph.NodeCount, Is.EqualTo(1));
            handles[16].Dispose();
            Assert.That(ReadField(node, "_index"), Is.Null);
            Assert.That(graph.NodeCount, Is.Zero);
        }

        // Mutations before and during row visits
        [TestCase(false)]
        [TestCase(true)]
        public void PredicateRemovalAndRecreationDefersTheRecreatedRowButKeepsOtherRows(bool removeCurrent)
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var changed = false;
            IDisposable current = null;
            IDisposable next = null;
            Func<int, bool> nextPredicate = _ => true;
            Func<int, bool> currentPredicate = null;
            currentPredicate = _ =>
            {
                if (changed) return true;
                changed = true;
                if (removeCurrent)
                {
                    current.Dispose();
                    stream.Where(currentPredicate).Subscribe(_ => calls.Add("replacement"));
                }
                else
                {
                    next.Dispose();
                    stream.Where(nextPredicate).Subscribe(_ => calls.Add("replacement"));
                }

                return true;
            };
            current = stream.Where(currentPredicate).Subscribe(_ => calls.Add("current"));
            next = stream.Where(nextPredicate).Subscribe(_ => calls.Add("next"));
            stream.Where(_ => true).Subscribe(_ => calls.Add("last"));

            stream.OnNext(1);
            Assert.That(calls, Is.EquivalentTo(new[] { "current", "next", "last" }));
            calls.Clear();
            stream.OnNext(2);
            Assert.That(calls, Is.EquivalentTo(removeCurrent
                ? new[] { "next", "last", "replacement" }
                : new[] { "current", "last", "replacement" }));
        }

        [Test]
        public void OrderedRowsAddedOnBothSidesOfTheCursorWaitAndRemovedRowsAreSkipped()
        {
            using var stream = new EventStream<int>();
            var calls = new List<int>();
            IDisposable current = null;
            IDisposable next = null;
            current = stream.ForOrder((Phase)0).Subscribe(_ =>
            {
                calls.Add(0);
                current.Dispose();
                next.Dispose();
                stream.ForOrder((Phase)(-1)).Subscribe(_ => calls.Add(-1));
                stream.ForOrder((Phase)1).Subscribe(_ => calls.Add(1));
            });
            next = stream.ForOrder((Phase)1).Subscribe(_ => calls.Add(99));
            stream.ForOrder((Phase)2).Subscribe(_ => calls.Add(2));

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { 0, 2 }));
            calls.Clear();
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(new[] { -1, 1, 2 }));
        }

        [Test]
        public void DebugFailureReleasesOnlyItsConnectionAndKeepsTheOwningRow()
        {
            using var stream = new EventStream<LogValue>();
            var failure = new ApplicationException("debug formatting");
            var reports = new List<Exception>();
            var calls = new List<string>();
            var path = stream.Where(_ => true);
            var failed = (Handle)path.Debug("failure", LogLevel.Warning).Subscribe(
                _ => calls.Add("failed"), () => calls.Add("complete"), _ => calls.Add("error"));
            path.Subscribe(_ => calls.Add("row"));
            stream.Where(_ => true).Subscribe(_ => calls.Add("sibling"));
            ExceptionHandler.SetHandler(reports.Add);
            try
            {
                stream.OnNext(new LogValue(failure));
                stream.OnNext(new LogValue(failure));
            }
            finally
            {
                ExceptionHandler.SetHandler(null);
            }

            Assert.That(failed.IsDisposed, Is.True);
            Assert.That(calls, Is.EqualTo(new[] { "row", "sibling", "row", "sibling" }));
            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(stream.NodeCount, Is.EqualTo(1));
        }

        // Depth and publication allocation
        [TestCase("Where", 0)]
        [TestCase("Where", 1)]
        [TestCase("Where", 2)]
        [TestCase("Flags", 0)]
        [TestCase("Flags", 1)]
        [TestCase("Flags", 2)]
        [TestCase("When", 0)]
        [TestCase("When", 1)]
        [TestCase("When", 2)]
        [TestCase("Order", 0)]
        [TestCase("Order", 1)]
        [TestCase("Order", 2)]
        [TestCase("Debug", 0)]
        [TestCase("Debug", 1)]
        [TestCase("Debug", 2)]
        public void MaximumDepthPublishesAndTerminatesEveryNodeKind(string kind, int ending)
        {
            using var stream = new EventStream<ulong>();
            var path = kind switch
            {
                "Where" => stream.Where(_ => true),
                "Flags" => stream.ForFlags(EventKinds.First),
                "When" => stream.When(1),
                "Order" => stream.ForOrder(Phase.First),
                _ => stream.Debug("depth", LogLevel.Warning)
            };
            for (var depth = 1; depth < 256; depth++) path = AddStep(path, kind);
            var calls = 0;
            var completed = 0;
            var errors = 0;
            var handle = (Handle)path.Subscribe(_ => calls++, () => completed++, _ => errors++);
            Assert.That(path.Depth, Is.EqualTo(256));
            Assert.That(stream.NodeCount, Is.EqualTo(256));
            Assert.Throws<InvalidOperationException>(() => AddStep(path, kind));
            var original = Console.Out;
            try
            {
                Console.SetOut(TextWriter.Null);
                stream.OnNext(1);
                if (ending == 0) stream.OnCompleted();
                else if (ending == 1) stream.OnError(new ApplicationException("terminal"));
                else stream.Dispose();
            }
            finally
            {
                Console.SetOut(original);
            }

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(completed, Is.EqualTo(ending == 0 ? 1 : 0));
            Assert.That(errors, Is.EqualTo(ending == 1 ? 1 : 0));
            Assert.That(handle.IsDisposed && stream.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.Zero);
        }

        [TestCase(16)]
        [TestCase(17)]
        public void FixedRowsAllocateNothingOnFirstAndRepeatedPublication(int count)
        {
            using var warm = CreateWideGraph(count);
            Publish(warm, 3000);
            using var measured = CreateWideGraph(count);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Publish(measured, 1);
            var first = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            Publish(measured, 10000);
            var repeated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(first, Is.Zero);
            Assert.That(repeated, Is.Zero);
            TestContext.WriteLine($"Keyed rows ({count}): first={first}, repeated={repeated} bytes.");
        }

        // Helpers
        private static EventGraph<ulong>.Connection Find(
            EventGraph<ulong> graph,
            EventGraph<ulong>.Connection parent,
            string kind,
            ulong key,
            Func<ulong, bool> predicate) => kind switch
            {
                "Where" => graph.FindOrAddWhere(parent, predicate),
                "Flags" => graph.FindOrAddFlags(parent, key),
                "When" => graph.FindOrAddWhen(parent, key),
                _ => graph.FindOrAddGroup(parent, (int)key)
            };

        private static object ReadField(object owner, string name) => owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static EventPath<ulong> AddStep(EventPath<ulong> path, string kind) => kind switch
        {
            "Where" => path.Where(_ => true),
            "Flags" => path.ForFlags(EventKinds.First),
            "When" => path.When(1),
            "Order" => path.ForOrder(Phase.First),
            _ => path.Debug("depth", LogLevel.Warning)
        };

        private static EventStream<ulong> CreateWideGraph(int count)
        {
            var stream = new EventStream<ulong>();
            for (var index = 0; index < count; index++)
            {
                var key = (ulong)index + 1;
                stream.Where(value => value == key).Subscribe(_ => { });
                stream.ForFlags((EventKinds)key).Subscribe(_ => { });
                stream.When(key).Subscribe(_ => { });
                stream.ForOrder((Phase)index).Subscribe(_ => { });
            }

            return stream;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Publish(EventStream<ulong> stream, int count)
        {
            for (var index = 0; index < count; index++) stream.OnNext(index % 2 == 0 ? 1UL : ulong.MaxValue);
        }

        [Flags]
        private enum EventKinds : ulong { First = 1, Second = 2, Third = 4 }

        private enum Phase { First }

        private class LogValue
        {
            private readonly Exception _failure;

            internal LogValue(Exception failure) => _failure = failure;
            public override string ToString() => throw _failure;
        }
    }
}
