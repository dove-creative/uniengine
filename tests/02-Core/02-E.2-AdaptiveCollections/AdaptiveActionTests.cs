using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class AdaptiveActionTests
    {
        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // R1: Removing the active multicast entry skips its remaining targets.
        [Test]
        public void AdaptiveActionSelfRemovalStopsTheEntryAndDefersAdditions()
        {
            var actions = new AdaptiveAction();
            var calls = new List<string>();
            var entry = (Action)null;

            entry = () =>
            {
                calls.Add("first");
                actions.Remove(entry);
                actions.Add(() => calls.Add("late"));
            };

            entry += () => calls.Add("tail");
            actions.Add(entry);
            actions.Add(() => calls.Add("other"));

            actions.Invoke();
            Assert.That(calls, Is.EqualTo(new[] { "first", "other" }));
            calls.Clear();
            actions.Invoke();
            Assert.That(calls, Is.EqualTo(new[] { "other", "late" }));
        }

        // R1: Callback failure removes its entry and always releases the outer enumeration.
        [TestCase(false)]
        [TestCase(true)]
        public void AdaptiveActionExceptionPolicyAndPendingChangesArePreserved(bool handled)
        {
            var actions = new AdaptiveAction();
            var calls = new List<string>();
            var failure = new ApplicationException("callback");

            var reported = new List<Exception>();
            if (handled) ExceptionHandler.SetHandler(reported.Add);

            var bad = (Action)(() =>
            {
                calls.Add("bad");
                actions.Add(() => calls.Add("late"));
                throw failure;
            });

            bad += () => calls.Add("tail");
            actions.Add(bad);
            actions.Add(() => calls.Add("other"));

            if (handled)
            {
                actions.Invoke();
                Assert.That(reported, Is.EqualTo(new[] { failure }));
            }
            else
            {
                Assert.That(Assert.Throws<ApplicationException>(() => actions.Invoke()), Is.SameAs(failure));
            }

            Assert.That(calls, Is.EqualTo(handled ? new[] { "bad", "other" } : new[] { "bad" }));
            calls.Clear();
            actions.Invoke();
            Assert.That(calls, Is.EqualTo(new[] { "other", "late" }));
        }

        // R1: A recursive Invoke fails without corrupting subsequent invocations.
        [Test]
        public void AdaptiveActionReentryLeavesTheCollectionUsable()
        {
            var actions = new AdaptiveAction();
            var otherCalls = 0;
            actions.Add(() => actions.Invoke());
            actions.Add(() => otherCalls++);
            Assert.Throws<InvalidOperationException>(() => actions.Invoke());
            actions.Invoke();
            Assert.That(otherCalls, Is.EqualTo(1));
        }

        // R1: Each generic consumer still forwards arguments through the new outer cursor.
        [Test]
        public void AdaptiveActionGenericVariantsPreserveArguments()
        {
            var calls = new List<string>();
            var one = new AdaptiveAction<int> { null, value => calls.Add($"{value}") };
            var two = new AdaptiveAction<int, string> { null, (value, text) => calls.Add($"{value}:{text}") };

            var three = new AdaptiveAction<int, string, bool>
            {
                null,
                (value, text, flag) => calls.Add($"{value}:{text}:{flag}")
            };

            one.Invoke(1);
            two.Invoke(2, "two");
            three.Invoke(3, "three", true);
            Assert.That(calls, Is.EqualTo(new[] { "1", "2:two", "3:three:True" }));
        }
    }
}
