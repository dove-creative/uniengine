using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamWhenTests
    {
        // Content --------------------
        // Equality and sharing
        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        [TestCase(0, 0, true)]
        [TestCase(1, 1, true)]
        [TestCase(2, 1, false)]
        public void ValueTypesUseDefaultEquality(int value, int target, bool matches)
        {
            AssertMatching(target, value, matches);
            AssertMatching(new ValueKey(target), new ValueKey(value), matches);
            AssertMatching((Phase)target, (Phase)value, matches);
        }

        [TestCase(null, null, true)]
        [TestCase(null, "value", false)]
        [TestCase("value", null, false)]
        [TestCase("value", "value", true)]
        [TestCase("value", "other", false)]
        public void StringsSupportValueEqualityAndNull(string target, string value, bool matches)
        {
            var separateValue = value == null ? null : new string(value.ToCharArray());
            AssertMatching(target, separateValue, matches);
        }

        [TestCase(null, null, true)]
        [TestCase(null, 1, false)]
        [TestCase(1, null, false)]
        [TestCase(1, 1, true)]
        [TestCase(1, 2, false)]
        public void NullableValuesSupportNullTargetsAndInputs(int? target, int? value, bool matches) =>
            AssertMatching(target, value, matches);

        [Test]
        public void ReferenceTypesUseTheirDefaultComparerAndPreserveTheInputReference()
        {
            var target = new ReferenceKey(7);
            var value = new ReferenceKey(7);
            AssertMatching(target, value, true);
            AssertMatching(target, new ReferenceKey(8), false);
            AssertMatching<ReferenceKey>(null, null, true);
            AssertMatching(target, null, false);
            AssertMatching<ReferenceKey>(null, value, false);

            var identity = new object();
            AssertMatching(identity, identity, true);
            AssertMatching(identity, new object(), false);
        }

        [Test]
        public void EqualTargetsShareAndDifferentTargetsOrParentsRemainSeparate()
        {
            using var stream = new EventStream<ReferenceKey>();
            var path = stream.When(new ReferenceKey(1));
            Assert.That(stream.NodeCount, Is.Zero);
            path.Subscribe(_ => { });
            stream.When(new ReferenceKey(1)).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            stream.When(new ReferenceKey(2)).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            path.When(new ReferenceKey(1)).Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(2));
        }

        [Test]
        public void NullEntriesShareWithEachOtherAndRemainSeparateFromNonNullEntries()
        {
            using var stream = new EventStream<string>();
            var values = new List<string>();
            stream.When(null).Subscribe(values.Add);
            stream.When(null).Subscribe(values.Add);
            stream.When("value").Subscribe(values.Add);

            Assert.That(stream.NodeCount, Is.EqualTo(1));
            stream.OnNext(null);
            stream.OnNext("value");
            Assert.That(values, Is.EqualTo(new string[] { null, null, "value" }));
        }

        [Test]
        public void PublicationLooksUpOnlyTheMatchingTarget()
        {
            using var stream = new EventStream<ReferenceKey>();
            var targets = Enumerable.Range(0, 64).Select(value => new ReferenceKey(value)).ToArray();
            var calls = 0;
            foreach (var target in targets) stream.When(target).Subscribe(_ => calls++);
            foreach (var target in targets) target.Comparisons = 0;

            stream.OnNext(new ReferenceKey(31));

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(targets[31].Comparisons, Is.EqualTo(1));
            Assert.That(targets.Where(target => target.Value != 31).Sum(target => target.Comparisons), Is.Zero);
        }

        // Ordering
        [TestCase(false)]
        [TestCase(true)]
        public void WhenKeepsItsPositionAmongWhereFlagsOrderAndConnections(bool whenFirst)
        {
            using var stream = new EventStream<ulong>();
            using var branch = stream.Branch();
            var calls = new List<string>();
            if (whenFirst) stream.When(1).Subscribe(_ => calls.Add("when"));
            stream.Where(_ => true).Subscribe(_ => calls.Add("where"));
            stream.ForFlags(EventKinds.First).Subscribe(_ => calls.Add("flags"));
            stream.ForOrder(Phase.Update).Subscribe(_ => calls.Add("order"));
            branch.Subscribe(_ => calls.Add("connection"));
            if (!whenFirst) stream.When(1).Subscribe(_ => calls.Add("when"));
            stream.When(2).Subscribe(_ => calls.Add("other-target"));
            stream.When(1).Subscribe(_ => calls.Add("when-again"));
            stream.Subscribe(_ => calls.Add("root"));

            stream.OnNext(1);

            var expected = whenFirst
                ? new[] { "root", "when", "when-again", "where", "flags", "order", "connection" }
                : new[] { "root", "where", "flags", "order", "connection", "when", "when-again" };
            Assert.That(calls, Is.EqualTo(expected));
        }

        [Test]
        public void SubscribersAtTheSamePositionRunBeforeExistingWhenChildren()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var path = stream.When(1);
            path.When(1).Subscribe(_ => calls.Add("nested"));
            path.Where(_ => true).Subscribe(_ => calls.Add("where"));
            path.Subscribe(_ => calls.Add("entry"));
            stream.Subscribe(_ => calls.Add("root"));

            stream.OnNext(1);

            Assert.That(calls, Is.EqualTo(new[] { "root", "entry", "nested", "where" }));
        }

        [Test]
        public void OrderGroupsKeepSeparateWhenContainersAndVisitInOrder()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            stream.ForOrder(Phase.Render).When(1).Subscribe(_ => calls.Add("render"));
            stream.ForOrder(Phase.Update).When(1).Subscribe(_ => calls.Add("update"));
            stream.ForOrder(Phase.Update).When(1).Subscribe(_ => calls.Add("update-again"));

            Assert.That(stream.NodeCount, Is.EqualTo(3));
            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "update", "update-again", "render" }));
        }

        [TestCase(null)]
        [TestCase("first")]
        public void RecreatedEntriesMoveToTheEndOfTerminalDeliveryOrder(string target)
        {
            using var stream = new EventStream<string>();
            var calls = new List<string>();
            var old = stream.When(target).Subscribe(_ => { }, () => calls.Add("old"), null);
            stream.When("second").Subscribe(_ => { }, () => calls.Add("second"), null);
            old.Dispose();
            stream.When(target).Subscribe(_ => { }, () => calls.Add("replacement"), null);

            stream.OnCompleted();

            Assert.That(calls, Is.EqualTo(new[] { "second", "replacement" }));
        }

        // Publication changes and cleanup
        [TestCase(false)]
        [TestCase(true)]
        public void ContainersAndEntriesAddedDuringPublicationWaitForTheNextValue(bool existingContainer)
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var added = false;
            if (existingContainer) stream.When(2).Subscribe(_ => calls.Add("other"));
            stream.Subscribe(_ =>
            {
                calls.Add("root");
                if (added) return;
                added = true;
                stream.When(1).Subscribe(_ => calls.Add("new"));
            });

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "root" }));
            calls.Clear();
            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "root", "new" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SubscribersAddedBeforeOrDuringTheirEntryWaitForTheNextValue(bool beforeEntry)
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var added = false;
            Action<int> add = _ =>
            {
                calls.Add("first");
                if (added) return;
                added = true;
                stream.When(1).Subscribe(_ => calls.Add("late"));
            };
            if (beforeEntry) stream.Subscribe(add);
            else stream.When(1).Subscribe(add);
            stream.When(1).Subscribe(_ => calls.Add("existing"));

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "first", "existing" }));
            calls.Clear();
            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "first", "existing", "late" }));
        }

        [TestCase(null)]
        [TestCase("value")]
        public void RemovedAndRecreatedEntriesWaitForTheNextValue(string target)
        {
            using var stream = new EventStream<string>();
            var calls = new List<string>();
            var old = stream.When(target).Subscribe(_ => calls.Add("old"));
            stream.When("unmatched").Subscribe(_ => calls.Add("unmatched"));
            var replaced = false;
            stream.Subscribe(_ =>
            {
                if (replaced) return;
                replaced = true;
                old.Dispose();
                stream.When(target).Subscribe(_ => calls.Add("replacement"));
            });
            stream.Where(_ => true).Subscribe(_ => calls.Add("sibling"));

            stream.OnNext(target);
            Assert.That(calls, Is.EquivalentTo(new[] { "old", "sibling" }));
            calls.Clear();
            stream.OnNext(target);
            Assert.That(calls, Is.EquivalentTo(new[] { "replacement", "sibling" }));
            Assert.That(stream.NodeCount, Is.EqualTo(2));
        }

        [Test]
        public void RemovingTheCurrentEntryAndItsLaterSubscriberPreservesOtherSiblings()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            IDisposable current = null;
            IDisposable later = null;
            current = stream.When(1).Subscribe(_ =>
            {
                calls.Add("current");
                current.Dispose();
                later.Dispose();
            });
            later = stream.When(1).Subscribe(_ => calls.Add("removed"));
            stream.Where(_ => true).Subscribe(_ => calls.Add("remaining"));

            stream.OnNext(1);
            stream.OnNext(1);

            Assert.That(calls, Is.EquivalentTo(new[] { "current", "removed", "remaining", "remaining" }));
            Assert.That(stream.NodeCount, Is.EqualTo(1));
        }

        [TestCase(null)]
        [TestCase("value")]
        public void LastSubscriberPrunesTheIndexAndTheBaseStreamCanBeReused(string target)
        {
            using var stream = new EventStream<string>();
            var first = stream.When(target).Subscribe(_ => { });
            var duplicate = stream.When(target).Subscribe(_ => { });
            var second = stream.When("other").Subscribe(_ => { });
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            first.Dispose();
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            duplicate.Dispose();
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            var values = new List<string>();
            var replacement = stream.When(target).Subscribe(values.Add);
            stream.OnNext(target);
            Assert.That(values, Is.EqualTo(new[] { target }));
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            second.Dispose();
            replacement.Dispose();
            Assert.That(stream.NodeCount, Is.Zero);
            Assert.That(stream.IsDisposed, Is.False);
            stream.When(target).Subscribe(values.Add);
            stream.OnNext(target);
            Assert.That(values, Is.EqualTo(new[] { target, target }));
            Assert.That(stream.NodeCount, Is.EqualTo(1));
        }

        [Test]
        public void EmptyWhenRootDisposesARelationAndRemovesItsUpstreamConnection()
        {
            using var parent = new EventStream<int>();
            using var branch = parent.When(1).Branch();
            var subscription = branch.When(1).Subscribe(_ => { });
            Assert.That(parent.NodeCount, Is.EqualTo(2));
            Assert.That(branch.NodeCount, Is.EqualTo(1));

            subscription.Dispose();

            Assert.That(branch.IsDisposed, Is.True);
            Assert.That(branch.NodeCount, Is.Zero);
            Assert.That(parent.NodeCount, Is.Zero);
            Assert.That(parent.IsDisposed, Is.False);
        }

        // Termination, composition, and failures
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void CompletionErrorAndDisposalReachEveryTargetIncludingNull(int ending)
        {
            using var stream = new EventStream<string>();
            var completed = new List<string>();
            var errors = new List<Exception>();
            var values = new List<string>();
            var failure = new ApplicationException("terminal");
            var first = (Handle)stream.When(null).Subscribe(values.Add, () => completed.Add("null"), errors.Add);
            using var branch = stream.When("branch").Branch();
            var second = (Handle)branch.When("other")
                .Subscribe(values.Add, () => completed.Add("branch"), errors.Add);
            stream.OnNext("unmatched");

            if (ending == 0) stream.OnCompleted();
            else if (ending == 1) stream.OnError(failure);
            else stream.Dispose();

            Assert.That(values, Is.Empty);
            Assert.That(completed, Is.EqualTo(ending == 0 ? new[] { "null", "branch" } : Array.Empty<string>()));
            Assert.That(errors, Is.EqualTo(ending == 1 ? new[] { failure, failure } : Array.Empty<Exception>()));
            Assert.That(first.IsDisposed && second.IsDisposed && branch.IsDisposed && stream.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.Zero);
            Assert.That(branch.NodeCount, Is.Zero);
        }

        [Test]
        public void WhenComposesWithWhereOrderSkipTakeAndBranchAtEachPosition()
        {
            using var source = new EventStream<int>();
            using var before = source.Skip(1);
            var checks = 0;
            var path = before.Where(value => value > 0).When(3).Where(_ =>
            {
                checks++;
                return true;
            });
            using var after = path.ForOrder(Phase.Render).Skip(1).Take(2);
            using var branch = after.When(3).Branch();
            var calls = new List<string>();
            branch.Subscribe(value => calls.Add($"render:{value}"), () => calls.Add("complete"), null);
            path.ForOrder(Phase.Update).Subscribe(value => calls.Add($"update:{value}"));

            foreach (var value in new[] { 3, 0, 2, 3, 3, 3, 3 }) source.OnNext(value);

            Assert.That(checks, Is.EqualTo(4));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "update:3", "update:3", "render:3", "update:3", "render:3", "complete", "update:3"
            }));
            Assert.That(after.IsDisposed && branch.IsDisposed, Is.True);
        }

        [Test]
        public void DebugBeforeAndAfterWhenObservesItsOwnPassingValues()
        {
            using var source = new EventStream<int>();
            var values = new List<int>();
            source.Debug("before", LogLevel.Warning).When(3)
                .Debug("after", LogLevel.Warning).Subscribe(values.Add);
            using var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                source.OnNext(2);
                source.OnNext(3);
            }
            finally
            {
                Console.SetOut(original);
            }

            Assert.That(values, Is.EqualTo(new[] { 3 }));
            Assert.That(output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
                Is.EqualTo(new[] { "2: before", "3: before", "3: after" }));
        }

        [Test]
        public void WhenAndForFlagsUseDifferentNodesAndDifferentMatchingRules()
        {
            using var stream = new EventStream<ulong>();
            var calls = new List<string>();
            stream.When(1).Subscribe(value => calls.Add($"when:{value}"));
            stream.ForFlags(EventKinds.First).Subscribe(value => calls.Add($"flags:{value}"));
            Assert.That(stream.NodeCount, Is.EqualTo(2));

            stream.OnNext(1);
            stream.OnNext(3);

            Assert.That(calls, Is.EqualTo(new[] { "when:1", "flags:1", "flags:3" }));
        }

        [Test]
        public void EachWhenOperationConsumesOnePathDepthAndOneGraphNode()
        {
            using var source = new EventStream<int>();
            var path = source.When(1);
            for (var depth = 1; depth < 256; depth++) path = path.When(1);
            var values = new List<int>();
            using var subscription = path.Subscribe(values.Add);
            Assert.That(path.Depth, Is.EqualTo(256));
            Assert.That(source.NodeCount, Is.EqualTo(256));
            Assert.Throws<InvalidOperationException>(() => path.When(1));

            source.OnNext(1);

            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(true, false)]
        public void PublicationComparisonFailuresReleaseTheWholeContainerAndFollowTheFailurePolicy(bool hash, bool handled)
        {
            using var stream = new EventStream<ReferenceKey>();
            var target = new ReferenceKey(1);
            var input = new ReferenceKey(1);
            var failure = new ApplicationException("comparison");
            var reports = new List<Exception>();
            var notifications = new List<string>();
            var calls = 0;
            var first = (Handle)stream.When(target).Subscribe(
                _ => notifications.Add("value"), () => notifications.Add("complete"), _ => notifications.Add("error"));
            var other = (Handle)stream.When(new ReferenceKey(2)).Subscribe(_ => notifications.Add("other"));
            var empty = (Handle)stream.When(null).Subscribe(_ => notifications.Add("null"));
            using var branch = stream.When(new ReferenceKey(3)).Branch();
            var nested = (Handle)branch.Subscribe(_ => notifications.Add("branch"));
            stream.Where(_ => true).Subscribe(_ => calls++);
            if (hash) input.HashFailure = failure;
            else target.EqualityFailure = failure;
            if (handled) ExceptionHandler.SetHandler(reports.Add);

            if (handled) Assert.DoesNotThrow(() => stream.OnNext(input));
            else Assert.That(Assert.Throws<ApplicationException>(() => stream.OnNext(input)), Is.SameAs(failure));

            Assert.That(notifications, Is.Empty);
            Assert.That(reports, Is.EqualTo(handled ? new[] { failure } : Array.Empty<Exception>()));
            Assert.That(first.IsDisposed && other.IsDisposed && empty.IsDisposed && nested.IsDisposed, Is.True);
            Assert.That(branch.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(handled ? 1 : 0));
            stream.OnNext(new ReferenceKey(1));
            Assert.That(calls, Is.EqualTo(handled ? 2 : 1));
            stream.When(new ReferenceKey(1)).Subscribe(_ => calls++);
            stream.OnNext(new ReferenceKey(1));
            Assert.That(calls, Is.EqualTo(handled ? 4 : 3));
        }

        [Test]
        public void QueuedRegistrationsSurviveTheOldWhenIndexFailingInTheSamePublication()
        {
            using var stream = new EventStream<ReferenceKey>();
            var failure = new ApplicationException("publication hash");
            var reports = new List<Exception>();
            var calls = 0;
            var queued = false;
            var old = (Handle)stream.When(new ReferenceKey(1)).Subscribe(_ => Assert.Fail("Failed route"));
            stream.Subscribe(_ =>
            {
                if (queued) return;
                queued = true;
                stream.When(new ReferenceKey(2)).Subscribe(_ => calls++);
                stream.When(new ReferenceKey(2)).Subscribe(_ => calls++);
            });
            ExceptionHandler.SetHandler(reports.Add);

            stream.OnNext(new ReferenceKey(1) { HashFailure = failure });
            Assert.That(old.IsDisposed, Is.True);
            Assert.That(calls, Is.Zero);
            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            stream.OnNext(new ReferenceKey(2));
            Assert.That(calls, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RegistrationHashFailuresLeaveNoNewEmptyAncestors(bool nested)
        {
            using var stream = new EventStream<ReferenceKey>();
            var failure = new ApplicationException("registration hash");
            var target = new ReferenceKey(1) { HashFailure = failure };
            var calls = 0;
            stream.Where(_ => true).Subscribe(_ => calls++);
            var path = nested
                ? stream.ForOrder(Phase.Render).Where(_ => true).When(new ReferenceKey(2)).When(target)
                : stream.When(target);

            Assert.That(Assert.Throws<ApplicationException>(() => path.Subscribe(_ => { })), Is.SameAs(failure));
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            stream.OnNext(new ReferenceKey(1));
            Assert.That(calls, Is.EqualTo(1));
            stream.When(new ReferenceKey(1)).Subscribe(_ => calls++);
            stream.OnNext(new ReferenceKey(1));
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void RegistrationEqualityFailuresPreserveExistingEntries()
        {
            using var stream = new EventStream<ReferenceKey>();
            var failure = new ApplicationException("registration equality");
            var target = new ReferenceKey(1);
            var values = new List<ReferenceKey>();
            var subscription = (Handle)stream.When(target).Subscribe(values.Add);
            target.EqualityFailure = failure;

            Assert.That(Assert.Throws<ApplicationException>(() =>
                stream.When(new ReferenceKey(1)).Subscribe(_ => { })), Is.SameAs(failure));
            Assert.That(subscription.IsDisposed, Is.False);
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            target.EqualityFailure = null;
            var input = new ReferenceKey(1);
            stream.OnNext(input);
            Assert.That(values.Count, Is.EqualTo(1));
            Assert.That(values[0], Is.SameAs(input));
        }

        [Test]
        public void FailedFirstRegistrationDisposesAPendingBranchAndRemovesItsSourceConnection()
        {
            using var source = new EventStream<ReferenceKey>();
            using var branch = source.Branch();
            var failure = new ApplicationException("pending registration");
            var target = new ReferenceKey(1) { HashFailure = failure };

            Assert.That(Assert.Throws<ApplicationException>(() => branch.When(target).Subscribe(_ => { })),
                Is.SameAs(failure));

            Assert.That(source.NodeCount, Is.Zero);
            Assert.That(branch.NodeCount, Is.Zero);
            Assert.That(branch.IsDisposed, Is.True);
            Assert.That(source.IsDisposed, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PersistentComparisonFailureReleasesNestedWhenWithoutComparingAgain(bool hash)
        {
            using var stream = new EventStream<ReferenceKey>();
            var target = new ReferenceKey(1);
            var failure = new ApplicationException("persistent comparison");
            var notifications = new List<string>();
            var reports = new List<Exception>();
            var calls = 0;
            var subscription = (Handle)stream.When(target).When(target).Subscribe(
                _ => notifications.Add("value"), () => notifications.Add("complete"), _ => notifications.Add("error"));
            stream.Where(_ => true).Subscribe(_ => calls++);
            if (hash) target.HashFailure = failure;
            else target.EqualityFailure = failure;
            ExceptionHandler.SetHandler(reports.Add);

            Assert.DoesNotThrow(() => stream.OnNext(target));

            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(notifications, Is.Empty);
            Assert.That(subscription.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1));
            stream.OnNext(target);
            Assert.That(calls, Is.EqualTo(2));
        }

        // Helpers
        private static void AssertMatching<T>(T target, T value, bool matches)
        {
            using var stream = new EventStream<T>();
            var values = new List<T>();
            stream.When(target).Subscribe(values.Add);
            stream.OnNext(value);
            Assert.That(values, Is.EqualTo(matches ? new[] { value } : Array.Empty<T>()));
            if (matches && !typeof(T).IsValueType) Assert.That(values[0], Is.SameAs(value));
        }

        private readonly struct ValueKey : IEquatable<ValueKey>
        {
            private readonly int _value;

            public ValueKey(int value) => _value = value;
            public bool Equals(ValueKey other) => _value == other._value;
            public override bool Equals(object other) => other is ValueKey key && Equals(key);
            public override int GetHashCode() => _value;
        }

        private class ReferenceKey : IEquatable<ReferenceKey>
        {
            public int Value { get; }
            public int Comparisons;
            public Exception HashFailure;
            public Exception EqualityFailure;

            public ReferenceKey(int value) => Value = value;

            public bool Equals(ReferenceKey other)
            {
                Comparisons++;
                if (EqualityFailure != null) throw EqualityFailure;
                return other != null && Value == other.Value;
            }

            public override bool Equals(object other) => other is ReferenceKey key && Equals(key);
            public override int GetHashCode() => HashFailure == null ? Value : throw HashFailure;
        }

        [Flags]
        private enum EventKinds { First = 1, Second = 2 }

        private enum Phase { Update, Render }
    }
}
