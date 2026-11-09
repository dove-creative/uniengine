using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;
using Handle = Blackthunder.UniEngine.Core.Handle;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventStreamGraphPropagationTests
    {
        // Content --------------------
        // Mutation during publication
        [Test]
        public void SelfRemovalSkipsItsMulticastTailButPreservesAnIdenticalGraphSubscription()
        {
            using var stream = new EventStream<int>();
            var path = stream.Where(value => value > 0);
            var calls = new List<string>();
            var first = (IDisposable)null;

            Action<int> updated = value =>
            {
                calls.Add($"head:{value}");
                first.Dispose();
            };

            updated += value => calls.Add($"tail:{value}");
            first = path.Subscribe(updated);
            path.Subscribe(updated);

            stream.OnNext(1);
            stream.OnNext(2);

            Assert.That(calls, Is.EqualTo(new[] { "head:1", "head:1", "tail:1", "head:2", "tail:2" }));
        }

        [Test]
        public void NewSubscribersAndNodesWaitWhileRemovalSkipsPendingCallbacksImmediately()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var existing = stream.Where(_ => true);
            var newChild = stream.Where(_ => true);
            var removed = (IDisposable)null;

            stream.Subscribe(value =>
            {
                calls.Add($"root:{value}");

                if (value != 1) return;

                removed.Dispose();
                stream.Subscribe(next => calls.Add($"lateRoot:{next}"));
                existing.Subscribe(next => calls.Add($"lateExisting:{next}"));
                newChild.Subscribe(next => calls.Add($"lateChild:{next}"));
                var cancelled = newChild.Subscribe(next => calls.Add($"cancelled:{next}"));
                cancelled.Dispose();
            });

            removed = existing.Subscribe(value => calls.Add($"removed:{value}"));
            existing.Subscribe(value => calls.Add($"existing:{value}"));

            stream.OnNext(1);
            stream.OnNext(2);

            Assert.That(calls, Is.EqualTo(new[]
            {
                "root:1", "existing:1", "root:2", "lateRoot:2", "existing:2", "lateExisting:2", "lateChild:2",
            }));
        }

        [Test]
        public void RemovingTheCurrentAndNextNodesStillReachesTheirSurvivingSibling()
        {
            using var stream = new EventStream<int>();
            var calls = new List<string>();
            var first = stream.Where(_ => true);
            var next = stream.Where(_ => true);
            var current = (IDisposable)null;
            var removed = (IDisposable)null;

            current = first.Subscribe(value =>
            {
                calls.Add($"first:{value}");
                current.Dispose();
                removed.Dispose();
                next.Subscribe(v => calls.Add($"replacement:{v}"));
            });

            removed = next.Subscribe(_ => calls.Add("removed"));
            stream.Where(_ => true).Subscribe(value => calls.Add($"last:{value}"));

            stream.OnNext(1);
            Assert.That(calls, Is.EqualTo(new[] { "first:1", "last:1" }));
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(new[] { "first:1", "last:1", "last:2", "replacement:2" }));
        }

        [Test]
        public void DisposalFromACallbackSilentlyStopsTheRestOfTheTraversal()
        {
            using var stream = new EventStream<int>();
            var notifications = 0;
            var first = (Handle)stream.Subscribe(_ => stream.Dispose());
            var later = (Handle)stream.Where(_ => true)
                .Subscribe(_ => notifications++, () => notifications++, _ => notifications++);
            stream.OnNext(1);
            Assert.That(notifications, Is.Zero);
            Assert.That(first.IsDisposed && later.IsDisposed, Is.True);
            Assert.That(stream.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.Zero);
        }

        // Failures
        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        [Test]
        public void UpstreamPreservesAnEarlierConnectedFailureAndFinishesDeliveringAfterADirectSubscriberFails()
        {
            using var source = new EventStream<int>();
            using var stream = new EventStream<int>(source);
            var graphFailure = new ApplicationException("graph");
            var directFailure = new InvalidOperationException("direct");
            var calls = new List<string>();
            var failedGraph = (Handle)stream.Subscribe(_ => throw graphFailure);
            stream.Subscribe(value => calls.Add($"graph:{value}"));
            var failedDirect = (Handle)source.Subscribe(_ => throw directFailure);
            source.Subscribe(value => calls.Add($"source:{value}"));

            Assert.That(Assert.Throws<ApplicationException>(() => source.OnNext(1)), Is.SameAs(graphFailure));
            source.OnNext(2);

            Assert.That(calls, Is.EqualTo(new[] { "graph:1", "source:1", "graph:2", "source:2" }));
            Assert.That(failedGraph.IsDisposed && failedDirect.IsDisposed, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CallbackFailureIsReportedOnceAndOnlyTheFailedSubscriberIsReleased(bool reenter)
        {
            using var stream = new EventStream<int>();
            var reported = new List<Exception>();
            ExceptionHandler.SetHandler(reported.Add);
            var failure = new ApplicationException("callback");
            var calls = new List<int>();
            var path = stream.Where(_ => true);

            var failed = (Handle)path.Subscribe(_ =>
            {
                if (reenter) stream.OnNext(9);
                else throw failure;
            });

            var healthy = (Handle)path.Subscribe(calls.Add);

            stream.OnNext(1);
            stream.OnNext(2);
            Assert.That(calls, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(reported, Has.Count.EqualTo(1));

            if (reenter) Assert.That(reported[0], Is.TypeOf<InvalidOperationException>());
            else Assert.That(reported[0], Is.SameAs(failure));

            Assert.That(failed.IsDisposed, Is.True);
            Assert.That(healthy.IsDisposed, Is.False);
            Assert.That(stream.NodeCount, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingReporterPreservesTheNextSiblingAndTheNextPublication()
        {
            using var stream = new EventStream<int>();
            var failure = new ApplicationException("subscriber");
            var reported = new List<Exception>();
            var reporterFailure = new InvalidOperationException("reporter");

            ExceptionHandler.SetHandler(exception =>
            {
                reported.Add(exception);
                throw reporterFailure;
            });

            var failed = (Handle)stream.Where(_ => true).Subscribe(_ => throw failure);
            var values = new List<int>();
            stream.Where(_ => true).Subscribe(values.Add);

            Assert.That(Assert.Throws<InvalidOperationException>(() => stream.OnNext(1)), Is.SameAs(reporterFailure));
            stream.OnNext(2);

            Assert.That(reported, Is.EqualTo(new[] { failure }));
            Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(failed.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstUnhandledFailureIsRethrownAfterBranchAndParentSiblingsReceiveTheValue(bool failFilter)
        {
            using var parent = new EventStream<int>();
            using var branch = parent.Branch();
            var first = new ApplicationException("first");
            var later = new InvalidOperationException("later");
            var values = new List<string>();
            var selected = branch.Where(_ => failFilter ? throw first : true);
            var failed = (Handle)selected.Subscribe(_ => throw first);
            var second = (Handle)branch.Where(_ => true).Subscribe(_ => throw later);
            branch.Where(_ => true).Subscribe(v => values.Add($"branch:{v}"));
            parent.Where(_ => true).Subscribe(v => values.Add($"parent:{v}"));

            Assert.That(Assert.Throws<ApplicationException>(() => parent.OnNext(1)), Is.SameAs(first));
            parent.OnNext(2);
            Assert.That(values, Is.EqualTo(new[] { "branch:1", "parent:1", "branch:2", "parent:2" }));
            Assert.That(failed.IsDisposed && second.IsDisposed, Is.True);
            Assert.That(branch.IsDisposed, Is.False);
        }

        [Test]
        public void PredicateFailureSilentlyReleasesItsWholeSubtreeAndVisitsTheNextSibling()
        {
            using var stream = new EventStream<int>();
            var reported = new List<Exception>();
            ExceptionHandler.SetHandler(reported.Add);
            var failure = new ApplicationException("filter");
            var path = stream.Where(_ => throw failure);
            var unexpected = 0;
            var failed = (Handle)path.Subscribe(_ => unexpected++, () => unexpected++, _ => unexpected++);
            var leaf = (Handle)path.Take(1).Subscribe(_ => unexpected++, () => unexpected++, _ => unexpected++);
            var healthy = new List<int>();
            stream.Where(_ => true).Subscribe(healthy.Add);

            stream.OnNext(1);
            stream.OnNext(2);
            Assert.That(reported, Is.EqualTo(new[] { failure }));
            Assert.That(unexpected, Is.Zero);
            Assert.That(failed.IsDisposed && leaf.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.EqualTo(1));
            Assert.That(healthy, Is.EqualTo(new[] { 1, 2 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RelationPredicateFailureSilentlyReleasesTheRelationAndKeepsItsSiblings(bool skip)
        {
            using var stream = new EventStream<int>();
            var reported = new List<Exception>();
            ExceptionHandler.SetHandler(reported.Add);
            var failure = new ApplicationException("filter");
            Func<int, bool> failing = _ => throw failure;
            using var relation = skip ? stream.SkipWhile(failing) : stream.TakeWhile(failing);
            var unexpected = 0;
            var failed = (Handle)relation.Subscribe(_ => unexpected++, () => unexpected++, _ => unexpected++);
            var healthy = new List<int>();
            stream.Subscribe(healthy.Add);

            stream.OnNext(1);
            stream.OnNext(2);

            Assert.That(reported, Is.EqualTo(new[] { failure }));
            Assert.That(unexpected, Is.Zero);
            Assert.That(failed.IsDisposed && relation.IsDisposed, Is.True);
            Assert.That(stream.NodeCount, Is.Zero);
            Assert.That(healthy, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void UnhandledRelationPredicateFailureIsRethrownAfterSiblingsReceiveTheValue()
        {
            using var stream = new EventStream<int>();
            var failure = new ApplicationException("filter");
            using var relation = stream.TakeWhile(_ => throw failure);
            relation.Subscribe(_ => { });
            var healthy = new List<int>();
            stream.Where(_ => true).Subscribe(healthy.Add);

            Assert.That(Assert.Throws<ApplicationException>(() => stream.OnNext(1)), Is.SameAs(failure));

            Assert.That(healthy, Is.EqualTo(new[] { 1 }));
            Assert.That(relation.IsDisposed, Is.True);
        }

        [Test]
        public void UnhandledFailureInTheLastTakeValueStillCompletesTheTake()
        {
            using var stream = new EventStream<int>();
            var failure = new ApplicationException("subscriber");
            using var taken = stream.Take(1);
            taken.Subscribe(_ => throw failure);
            var completions = 0;
            taken.Subscribe(_ => { }, () => completions++, null);

            Assert.That(Assert.Throws<ApplicationException>(() => stream.OnNext(1)), Is.SameAs(failure));

            Assert.That(completions, Is.EqualTo(1));
            Assert.That(taken.IsDisposed, Is.True);
        }

        // F3: A later completion failure must not replace the first value-reporting failure.
        [TestCase(false, "Unhandled")]
        [TestCase(true, "Unhandled")]
        [TestCase(false, "Handled")]
        [TestCase(true, "Handled")]
        [TestCase(false, "ThrowingReporter")]
        [TestCase(true, "ThrowingReporter")]
        public void FinalTakePreservesFirstFailureAcrossValueAndCompletion(bool directInput, string reporting)
        {
            using var source = new EventStream<int>();
            using var taken = source.Take(1);
            var valueFailure = new ApplicationException("value");
            var completionFailure = new ApplicationException("completion");
            var firstReportFailure = new InvalidOperationException("first report");
            var laterReportFailure = new InvalidOperationException("later report");
            var reports = new List<Exception>();
            var calls = new List<string>();
            var sourceValues = new List<int>();
            ExceptionHandler.SetHandler(null);

            if (reporting != "Unhandled")
            {
                ExceptionHandler.SetHandler(exception =>
                {
                    reports.Add(exception);
                    if (reporting == "ThrowingReporter")
                        throw exception == valueFailure ? firstReportFailure : laterReportFailure;
                });
            }

            var failedValue = (Handle)taken.Subscribe(_ => throw valueFailure);
            var failedCompletion = (Handle)taken.Subscribe(
                _ => { }, () => throw completionFailure, null);
            var healthy = (Handle)taken.Subscribe(
                value => calls.Add($"value:{value}"), () => calls.Add("completed"), null);
            source.Where(_ => true).Subscribe(sourceValues.Add);
            TestDelegate publish = () =>
            {
                if (directInput) taken.OnNext(1);
                else source.OnNext(1);
            };

            if (reporting == "Handled") Assert.DoesNotThrow(publish);
            else if (reporting == "ThrowingReporter")
                Assert.That(Assert.Throws<InvalidOperationException>(publish), Is.SameAs(firstReportFailure));
            else Assert.That(Assert.Throws<ApplicationException>(publish), Is.SameAs(valueFailure));

            Assert.That(reports, Is.EqualTo(reporting == "Unhandled"
                ? Array.Empty<Exception>() : new Exception[] { valueFailure, completionFailure }));
            Assert.That(calls, Is.EqualTo(new[] { "value:1", "completed" }));
            Assert.That(failedValue.IsDisposed && failedCompletion.IsDisposed && healthy.IsDisposed, Is.True);
            Assert.That(taken.IsDisposed, Is.True);
            Assert.That(taken.NodeCount, Is.Zero);
            Assert.That(source.NodeCount, Is.EqualTo(1), "Only the healthy sibling path remains.");
            source.OnNext(2);
            Assert.That(sourceValues, Is.EqualTo(directInput ? new[] { 2 } : new[] { 1, 2 }));
        }

        // F3: Capturing terminal failure must not swallow it when no value callback failed.
        [TestCase(false)]
        [TestCase(true)]
        public void FinalTakeStillPropagatesCompletionFailure(bool directInput)
        {
            using var source = new EventStream<int>();
            using var taken = source.Take(1);
            var failure = new ApplicationException("completion");
            var completed = 0;
            var values = new List<int>();
            var sourceValues = new List<int>();
            ExceptionHandler.SetHandler(null);
            var failed = (Handle)taken.Subscribe(_ => { }, () => throw failure, null);
            var healthy = (Handle)taken.Subscribe(values.Add, () => completed++, null);
            source.Where(_ => true).Subscribe(sourceValues.Add);

            var thrown = Assert.Throws<ApplicationException>(() =>
            {
                if (directInput) taken.OnNext(1);
                else source.OnNext(1);
            });

            Assert.That(thrown, Is.SameAs(failure));
            Assert.That(values, Is.EqualTo(new[] { 1 }));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(failed.IsDisposed && healthy.IsDisposed && taken.IsDisposed, Is.True);
            Assert.That(taken.NodeCount, Is.Zero);
            Assert.That(source.NodeCount, Is.EqualTo(1));
            source.OnNext(2);
            Assert.That(sourceValues, Is.EqualTo(directInput ? new[] { 2 } : new[] { 1, 2 }));
        }
    }
}
