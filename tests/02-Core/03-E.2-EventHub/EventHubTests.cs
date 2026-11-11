using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Blackthunder.UniEngine.Core;
using BlackThunder.UniEngine.Infrastructure;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventHubTests
    {
        // Content --------------------
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        // Connections
        [Test]
        public void ConnectRegistersANewListenerForUpdates()
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var values = new List<int>();

            hub.Connect(listener).OnUpdated(values.Add);
            hub.Update(1);

            Assert.That(hub.HasListener(listener), Is.True);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void ConnectRejectsADuplicatedListenerAndKeepsItsConnection()
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var values = new List<int>();
            hub.Connect(listener).OnUpdated(values.Add);

            Assert.Throws<InvalidOperationException>(() => hub.Connect(listener));
            hub.Update(1);

            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void ConnectRejectsANullListener()
        {
            using var hub = new EventHub<int>(new object());

            var exception = Assert.Throws<ArgumentNullException>(() => hub.Connect(null));

            Assert.That(exception.ParamName, Is.EqualTo("listener"));
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [Test]
        public void ConnectAfterDisposeThrows()
        {
            var hub = CreateLifecycleHub(out var dispose);
            dispose();

            Assert.That(hub.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => hub.Connect(new object()));
        }

        [Test]
        public void DisconnectRemovesTheListenerAndItsUpdates()
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var values = new List<int>();
            hub.Connect(listener).OnUpdated(values.Add);

            hub.Disconnect(listener);
            hub.Update(1);

            Assert.That(hub.HasListener(listener), Is.False);
            Assert.That(hub.ConnectionCount, Is.Zero);
            Assert.That(values, Is.Empty);
        }

        [Test]
        public void DisconnectedListenerIsNotNotifiedOnDispose()
        {
            var hub = CreateLifecycleHub(out var dispose);
            var listener = new object();
            var notifications = 0;
            hub.Connect(listener).OnDisposed(() => notifications++);

            hub.Disconnect(listener);
            dispose();

            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void DisconnectIgnoresUnknownAndNullListenersAndDisposedHubs()
        {
            var hub = CreateLifecycleHub(out var dispose);
            var listener = new object();
            var notifications = 0;
            hub.Connect(listener).OnDisposed(() => notifications++);

            hub.Disconnect(new object());
            hub.Disconnect(null);
            dispose();

            Assert.That(notifications, Is.EqualTo(1));
            Assert.DoesNotThrow(() => hub.Disconnect(listener));
        }

        // Updates
        [TestCase(false)]
        [TestCase(true)]
        public void FirstAndRepeatedTypedUpdatesAllocateNoManagedMemory(bool subscribe)
        {
            using var warm = CreateUpdateHub(subscribe, new Counter());
            for (var index = 0; index < 3000; index++) warm.Update(1);

            var counter = new Counter();
            using var measured = CreateUpdateHub(subscribe, counter);
            var before = GC.GetAllocatedBytesForCurrentThread();
            measured.Update(1);
            var firstBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            const int repetitions = 10000;
            before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < repetitions; index++) measured.Update(1);

            var repeatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(counter.Calls, Is.EqualTo(subscribe ? 6 * (repetitions + 1) : 0));
            Assert.That(firstBytes, Is.Zero);
            Assert.That(repeatedBytes, Is.Zero);
        }

        [Test]
        public void UpdateDefersBuilderAndDirectAdditionsAndSkipsDisconnectedCallbacks()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var early = hub.Connect(new object());
            var later = hub.Connect(new object());

            early.OnUpdated(value =>
            {
                calls.Add($"early:{value}");

                if (value != 1) return;

                later.OnUpdated(next => calls.Add($"added:{next}"));
                hub.Updated += next => calls.Add($"added direct:{next}");
                hub.Update(99);
            });

            later.OnUpdated(value => calls.Add($"later:{value}"));
            var removed = new object();

            hub.Connect(removed).OnUpdated(value =>
            {
                calls.Add($"filter:{value}");
                hub.Disconnect(removed);
                return true;
            }, value => calls.Add($"removed:{value}"));

            hub.Updated += value => calls.Add($"direct:{value}");

            hub.Update(1);
            hub.Update(2);

            Assert.That(calls, Is.EqualTo(new[]
            {
                "early:1", "later:1", "direct:1", "filter:1",
                "early:2", "later:2", "direct:2", "added:2", "added direct:2",
            }));
        }

        [TestCase(null)]
        [TestCase("args")]
        public void UpdateDeliversTheGivenArgsToListeners(string args)
        {
            using var hub = new EventHub<string>(new object());
            var received = new List<string>();
            hub.Connect(new object()).OnUpdated(received.Add);

            hub.Update(args);

            Assert.That(received, Is.EqualTo(new[] { args }));
        }

        [Test]
        public void UpdateAfterDisposeThrows()
        {
            var hub = new EventHub<int>(new object());
            var values = new List<int>();
            hub.Connect(new object()).OnUpdated(values.Add);
            hub.Dispose();

            Assert.Throws<ObjectDisposedException>(() => hub.Update(1));
            Assert.That(values, Is.Empty);
        }

        [Test]
        public void UpdateNotifiesRootCallbacksInRegistrationOrder()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            hub.Connect(new object()).OnUpdated(value => calls.Add($"first:{value}"));
            hub.Connect(new object()).OnUpdated(value => calls.Add($"second:{value}"));

            hub.Update(1);

            Assert.That(hub.ConnectionCount, Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(new[] { "first:1", "second:1" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReconnectedListenerUsesOnlyItsNewRegistration(bool disconnectAgain)
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var calls = new List<string>();
            hub.Connect(listener).OnUpdated(value => calls.Add($"stale:{value}"));
            hub.Disconnect(listener);
            hub.Connect(listener).OnUpdated(value => calls.Add($"fresh:{value}"));

            if (disconnectAgain) hub.Disconnect(listener);

            hub.Update(1);

            Assert.That(hub.HasListener(listener), Is.EqualTo(!disconnectAgain));
            Assert.That(calls, Is.EqualTo(disconnectAgain ? Array.Empty<string>() : new[] { "fresh:1" }));
        }

        [Test]
        public void ListenersDisconnectedDuringUpdateMissTheRestOfIt()
        {
            using var hub = new EventHub<int>(new object());
            var first = new object();
            var second = new object();
            var calls = new List<string>();

            hub.Connect(first).OnUpdated(_ =>
            {
                calls.Add("first");
                hub.Disconnect(first);
                hub.Disconnect(second);
            }).OnUpdated(_ => calls.Add("first tail"));

            hub.Connect(second).OnUpdated(_ => calls.Add("second"));

            hub.Update(1);
            hub.Update(2);

            Assert.That(hub.HasListener(first), Is.False);
            Assert.That(hub.HasListener(second), Is.False);
            Assert.That(calls, Is.EqualTo(new[] { "first" }));
        }

        [Test]
        public void DirectUpdatedRejectsDuplicatesAndRemovesItsConnection()
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            Action<int> record = values.Add;

            hub.Updated += record;
            Assert.Throws<InvalidOperationException>(() => hub.Updated += record);
            Assert.That(hub.HasListener(record), Is.True);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            hub.Update(1);
            hub.Updated -= record;
            hub.Update(2);
            hub.Updated -= record;
            hub.Update(3);

            Assert.That(hub.ConnectionCount, Is.Zero);
            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void DirectMulticastRemovalRequiresTheWholeRegistration(bool objectEvent, bool removeWhole)
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            Action<int> first = _ => calls.Add("first");
            Action<int> second = _ => calls.Add("second");
            Action<object> adaptedFirst = _ => calls.Add("first");
            Action<object> adaptedSecond = _ => calls.Add("second");

            if (objectEvent)
            {
                var untyped = (IEventHub)hub;
                untyped.Updated += adaptedFirst + adaptedSecond;
                untyped.Updated -= removeWhole ? adaptedFirst + adaptedSecond : adaptedSecond;
            }
            else
            {
                hub.Updated += first + second;
                hub.Updated -= removeWhole ? first + second : second;
            }

            hub.Update(1);
            Assert.That(calls, Is.EqualTo(removeWhole ? Array.Empty<string>() : new[] { "first", "second" }));
            Assert.That(hub.ConnectionCount, Is.EqualTo(removeWhole ? 0 : 1));
        }

        [Test]
        public void DirectRemovalDuringUpdateIgnoresAlreadyFailedSubscriptions()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            Action<int> first = _ => calls.Add("first");
            Action<int> second = _ => calls.Add("second");
            ExceptionHandler.SetHandler(_ => { });
            hub.Updated += first;
            hub.Updated += _ => throw new ApplicationException("removed");
            hub.Updated += second;
            hub.Updated += _ =>
            {
                calls.Add("remove");
                hub.Updated -= first;
                hub.Updated -= second;
            };

            hub.Update(1);
            hub.Update(2);

            Assert.That(calls, Is.EqualTo(new[] { "first", "second", "remove", "remove" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LastDirectUpdateRemovalDisconnectsImmediatelyDuringUpdate(bool objectEvent)
        {
            using var hub = new EventHub<object>(new object());
            var values = new List<object>();
            Action<object> callback = values.Add;
            var builder = hub.Connect(callback);
            var connectedAfterRemoval = true;

            hub.Updated += _ =>
            {
                if (objectEvent) ((IEventHub)hub).Updated -= callback;
                else hub.Updated -= callback;

                connectedAfterRemoval = hub.HasListener(callback);
            };

            if (objectEvent) ((IEventHub)hub).Updated += callback;
            else hub.Updated += callback;

            hub.Update(1);

            Assert.That(connectedAfterRemoval, Is.False);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            Assert.That(values, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(callback));
        }

        // Paths and builder lifetime
        [Test]
        public void ListenersShareTheSameWhereDelegateAndReleaseTheirOwnSubscriptions()
        {
            using var hub = new EventHub<int>(new object());
            var first = new object();
            var second = new object();
            var evaluations = 0;
            var values = new List<int>();
            Func<int, bool> predicate = value => { evaluations++; return value > 0; };
            var root = hub.Connect(first);

            Assert.That(root.OnUpdated(predicate, values.Add), Is.SameAs(root));
            hub.Connect(second).OnUpdated(EventCondition.Where(predicate), values.Add);
            hub.Update(1);
            hub.Update(0);
            hub.Disconnect(first);
            hub.Update(2);
            hub.Disconnect(second);
            hub.Update(3);

            Assert.That(evaluations, Is.EqualTo(3));
            Assert.That(values, Is.EqualTo(new[] { 1, 1, 2 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NullConditionsAreRejectedWithoutAffectingExistingUpdates(bool objectBuilder)
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var values = new List<int>();
            var builder = hub.Connect(listener).OnUpdated(values.Add);

            if (objectBuilder)
            {
                var untyped = (IEventHubBuilder)builder;
                Assert.Throws<ArgumentNullException>(() => untyped.OnUpdated(null, _ => { }));
                Assert.Throws<ArgumentNullException>(() => untyped.OnUpdated(null, () => { }));
                Assert.Throws<ArgumentNullException>(() => untyped.OnUpdated(null, (Action<object>)null));
                Assert.Throws<ArgumentNullException>(() => untyped.OnUpdated(null, (Action)null));
            }
            else
            {
                Assert.Throws<ArgumentNullException>(() => builder.OnUpdated((Func<int, bool>)null, _ => { }));
                Assert.Throws<ArgumentNullException>(() => builder.OnUpdated((Func<int, bool>)null, () => { }));
                Assert.Throws<ArgumentNullException>(() => builder.OnUpdated((EventCondition<int>)null, _ => { }));
                Assert.Throws<ArgumentNullException>(() => builder.OnUpdated((EventCondition<int>)null, () => { }));
                Assert.Throws<ArgumentNullException>(() => builder.OnUpdated((Func<int, bool>)null, (Action<int>)null));
                Assert.Throws<ArgumentNullException>(() => builder.OnUpdated((EventCondition<int>)null, (Action)null));
            }

            hub.Update(1);
            hub.Update(2);
            Assert.That(hub.HasListener(listener), Is.True);
            Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DebugForwardsPassingValuesUnchanged(bool parameterless)
        {
            using var hub = new EventHub<int>(new object());
            var builder = hub.Connect(new object());
            var values = new List<int>();

            var condition = EventCondition.Where<int>(value => value > 0).Debug("hub", LogLevel.Warning);
            if (parameterless) builder.OnUpdated(condition, () => values.Add(7));
            else builder.OnUpdated(condition, values.Add);

            using var output = new StringWriter();
            var original = Console.Out;
            try
            {
                Console.SetOut(output);
                hub.Update(0);
                hub.Update(7);
            }
            finally
            {
                Console.SetOut(original);
            }

            Assert.That(values, Is.EqualTo(new[] { 7 }));
            Assert.That(output.ToString(), Does.Contain("7: hub").And.Not.Contain("0: hub"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ParameterlessUpdatesWorkAtTheRootAndAfterWhere(bool objectBuilder)
        {
            using var hub = new EventHub<int>(new object());
            var builder = hub.Connect(new object());
            var rootCalls = 0;
            var pathCalls = 0;
            Action root = () => rootCalls++;
            Action path = () => pathCalls++;

            if (objectBuilder)
            {
                var untyped = (IEventHubBuilder)builder;
                Assert.That(untyped.OnUpdated(root), Is.SameAs(untyped));
                Assert.That(untyped.OnUpdated(value => (int)value > 0, path + path), Is.SameAs(builder));
            }
            else
            {
                Assert.That(builder.OnUpdated(root), Is.SameAs(builder));
                Assert.That(builder.OnUpdated(value => value > 0, path + path), Is.SameAs(builder));
            }

            hub.Update(0);
            Assert.That(rootCalls, Is.EqualTo(1));
            Assert.That(pathCalls, Is.Zero);
            hub.Update(1);
            Assert.That(rootCalls, Is.EqualTo(2));
            Assert.That(pathCalls, Is.EqualTo(2));
        }

        [Test]
        public void RootSubscriptionsPrecedeChildrenAndChildrenFollowCreationOrder()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var builder = hub.Connect(new object());
            var condition = EventCondition.Where<int>(_ => true);
            builder.OnUpdated(condition.Where(_ => true), () => calls.Add("grandchild"));
            builder.OnUpdated(condition, () => calls.Add("child"));
            builder.OnUpdated(_ => true, () => calls.Add("sibling"));
            builder.OnUpdated(() => calls.Add("root"));

            hub.Update(1);

            Assert.That(calls, Is.EqualTo(new[] { "root", "child", "grandchild", "sibling" }));
        }

        [Test]
        public void AllRegistrationOverloadsReturnTheSameBuilder()
        {
            using var hub = new EventHub<int>(new object());
            var builder = hub.Connect(new object());
            var untyped = (IEventHubBuilder)builder;
            var calls = 0;
            var condition = EventCondition.When(1);

            Assert.That(builder.OnUpdated(_ => calls++), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(() => calls++), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(1, _ => calls++), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(1, () => calls++), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(value => value > 0, _ => calls++), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(value => value > 0, () => calls++), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(condition, _ => calls++), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(condition, () => calls++), Is.SameAs(builder));
            Assert.That(untyped.OnUpdated(_ => calls++), Is.SameAs(builder));
            Assert.That(untyped.OnUpdated(() => calls++), Is.SameAs(builder));
            Assert.That(untyped.OnUpdated(value => (int)value > 0, _ => calls++), Is.SameAs(builder));
            Assert.That(untyped.OnUpdated(value => (int)value > 0, () => calls++), Is.SameAs(builder));
            Assert.That(builder.OnDisposing(() => calls++), Is.SameAs(builder));
            Assert.That(builder.OnDisposing(_ => calls++), Is.SameAs(builder));
            Assert.That(builder.OnDisposed(() => calls++), Is.SameAs(builder));
            Assert.That(builder.OnDisposed(_ => calls++), Is.SameAs(builder));
            Assert.That(untyped.OnDisposing(() => calls++), Is.SameAs(builder));
            Assert.That(untyped.OnDisposing(_ => calls++), Is.SameAs(builder));
            Assert.That(untyped.OnDisposed(() => calls++), Is.SameAs(builder));
            Assert.That(untyped.OnDisposed(_ => calls++), Is.SameAs(builder));
            Assert.That(((ILifecycleHubBuilder)builder).OnDisposed(() => calls++), Is.SameAs(builder));

            hub.Update(1);
            Assert.That(calls, Is.EqualTo(12));
            hub.Dispose();
            Assert.That(calls, Is.EqualTo(21));
        }

        [TestCase(null, false)]
        [TestCase(null, true)]
        [TestCase("match", false)]
        [TestCase("match", true)]
        public void TargetSubscriptionsUseDefaultEqualityIncludingNull(string target, bool parameterless)
        {
            using var hub = new EventHub<string>(new object());
            var builder = hub.Connect(new object());
            var received = new List<string>();

            if (parameterless) builder.OnUpdated(target, () => received.Add(target));
            else builder.OnUpdated(target, received.Add);

            hub.Update(null);
            hub.Update(new string("match".ToCharArray()));
            hub.Update("other");

            Assert.That(received, Is.EqualTo(new[] { target }));
        }

        [Test]
        public void ConditionsAreImmutableAndApplyStepsFromTheRoot()
        {
            using var hub = new EventHub<int>(new object());
            var builder = hub.Connect(new object());
            var positive = EventCondition.Where<int>(value => value > 0);
            var one = positive.When(1);
            var two = positive.When(2);
            var calls = new List<string>();

            Assert.That(one, Is.Not.SameAs(positive));
            Assert.That(two, Is.Not.SameAs(positive));
            builder.OnUpdated(positive, value => calls.Add($"positive:{value}"));
            builder.OnUpdated(one, value => calls.Add($"one:{value}"));
            builder.OnUpdated(two, value => calls.Add($"two:{value}"));
            hub.Update(0);
            hub.Update(1);
            hub.Update(2);

            Assert.That(calls, Is.EqualTo(new[] { "positive:1", "one:1", "positive:2", "two:2" }));
        }

        [Test]
        public void ConditionFactoriesRejectNullPredicatesAtConstruction()
        {
            var condition = EventCondition.When(1);

            Assert.Throws<ArgumentNullException>(() => EventCondition.Where<int>(null));
            Assert.Throws<ArgumentNullException>(() => condition.Where(null));

            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            hub.Connect(new object()).OnUpdated(condition, values.Add);
            hub.Update(1);
            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void ConditionDepthAllows256StepsAndRejectsTheNextStepDuringConstruction()
        {
            Func<int, bool> predicate = _ => true;
            var condition = EventCondition.Where(predicate);
            for (var depth = 1; depth < 256; depth++) condition = condition.Where(predicate);

            Assert.Throws<InvalidOperationException>(() => condition.Where(predicate));
            Assert.Throws<InvalidOperationException>(() => condition.When(1));
            Assert.Throws<InvalidOperationException>(() => condition.Debug());

            using var hub = new EventHub<int>(new object());
            var calls = 0;
            hub.Connect(new object()).OnUpdated(condition, () => calls++);
            hub.Update(1);
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(0, false)]
        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(3, true)]
        public void WhenConditionsAndTargetShortcutsShareEntriesAcrossListeners(int registrationKind, bool parameterless)
        {
            var counter = new Counter();
            var target = new EqualityProbe(1, counter);
            var value = new EqualityProbe(1, counter);
            var condition = EventCondition.When(target);
            var received = new List<EqualityProbe>();
            var listeners = new[] { new object(), new object() };
            using var hub = new EventHub<EqualityProbe>(new object());

            for (var index = 0; index < listeners.Length; index++)
            {
                var builder = hub.Connect(listeners[index]);
                var currentTarget = new EqualityProbe(1, counter);
                var currentCondition = registrationKind == 0 ? condition : EventCondition.When(currentTarget);
                var useShortcut = registrationKind == 2 || (registrationKind == 3 && index == 0);

                if (parameterless)
                {
                    if (useShortcut) builder.OnUpdated(currentTarget, () => received.Add(value));
                    else builder.OnUpdated(currentCondition, () => received.Add(value));
                }
                else
                {
                    if (useShortcut) builder.OnUpdated(currentTarget, received.Add);
                    else builder.OnUpdated(currentCondition, received.Add);
                }
            }

            counter.Calls = 0;
            hub.Update(value);
            hub.Update(value);

            Assert.That(counter.Calls, Is.EqualTo(2));
            Assert.That(received.Count, Is.EqualTo(4));
            foreach (var receivedValue in received) Assert.That(receivedValue, Is.SameAs(value));

            hub.Disconnect(listeners[0]);
            counter.Calls = 0;
            hub.Update(value);
            Assert.That(counter.Calls, Is.EqualTo(1));
            Assert.That(received.Count, Is.EqualTo(5));

            hub.Disconnect(listeners[1]);
            counter.Calls = 0;
            hub.Update(value);
            Assert.That(counter.Calls, Is.Zero);
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [TestCase(null)]
        [TestCase("match")]
        public void SeparateWhenConditionsShareTheirDescendantsAndKeepCallbacksIndependent(string target)
        {
            using var hub = new EventHub<string>(new object());
            var first = new object();
            var second = new object();
            var evaluations = 0;
            var received = new List<string>();
            Func<string, bool> predicate = _ => { evaluations++; return true; };
            var firstBuilder = hub.Connect(first);
            firstBuilder.OnUpdated(EventCondition.When(target).Where(predicate), received.Add);
            firstBuilder.OnUpdated(EventCondition.When(target).Where(predicate), () => received.Add("plain"));
            hub.Connect(second).OnUpdated(EventCondition.When(target).Where(predicate), received.Add);

            hub.Update("other");
            hub.Update(target);
            Assert.That(evaluations, Is.EqualTo(1));
            Assert.That(received, Is.EqualTo(new[] { target, "plain", target }));

            hub.Disconnect(first);
            hub.Update(target);
            Assert.That(evaluations, Is.EqualTo(2));
            Assert.That(received, Is.EqualTo(new[] { target, "plain", target, target }));

            hub.Disconnect(second);
            hub.Update(target);
            Assert.That(evaluations, Is.EqualTo(2));
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WhenConditionsRejectEquivalentTargetsAndShortcuts(bool parameterless)
        {
            using var hub = new EventHub<string>(new object());
            var builder = hub.Connect(new object());
            var calls = 0;
            Action<string> typed = _ => calls++;
            Action plain = () => calls++;
            var equalTarget = new string("match".ToCharArray());

            if (parameterless)
            {
                builder.OnUpdated("match", plain);
                Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(equalTarget, plain));
                Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(EventCondition.When("match"), plain));
                builder.OnUpdated(EventCondition.When("other"), plain);
                Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(EventCondition.When("other"), plain));
            }
            else
            {
                builder.OnUpdated("match", typed);
                Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(equalTarget, typed));
                Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(EventCondition.When("match"), typed));
                builder.OnUpdated(EventCondition.When("other"), typed);
                Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(EventCondition.When("other"), typed));
            }

            hub.Update("match");
            hub.Update("other");
            Assert.That(calls, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PredicateShortcutsAndConditionsRejectEquivalentDelegates(bool parameterless)
        {
            using var hub = new EventHub<int>(new object());
            var builder = hub.Connect(new object());
            var calls = 0;
            var predicate = new Func<int, bool>(IsPositive);
            var equivalent = new Func<int, bool>(IsPositive);
            Action<int> typed = _ => calls++;
            Action plain = () => calls++;

            Assert.That(predicate, Is.Not.SameAs(equivalent));
            Assert.That(predicate.Equals(equivalent), Is.True);

            if (parameterless)
            {
                builder.OnUpdated(predicate, plain);
                Assert.Throws<InvalidOperationException>(() =>
                    builder.OnUpdated(EventCondition.Where(predicate), plain));
                Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(equivalent, plain));
                builder.OnUpdated(value => value > 0, plain);
            }
            else
            {
                builder.OnUpdated(predicate, typed);
                Assert.Throws<InvalidOperationException>(() =>
                    builder.OnUpdated(EventCondition.Where(predicate), typed));
                Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(equivalent, typed));
                builder.OnUpdated(value => value > 0, typed);
            }

            hub.Update(1);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void ObjectConditionsCompareOriginalDelegatesAndKeepCallbackKindsSeparate()
        {
            using var hub = new EventHub<object>(new object());
            var builder = hub.Connect(new object());
            var untyped = (IEventHubBuilder)builder;
            var receiver = new ObjectConditionReceiver();
            var first = new Func<object, bool>(receiver.Accept);
            var equal = new Func<object, bool>(receiver.Accept);
            Action<object> callback = receiver.Receive;
            Action plain = receiver.Tick;

            Assert.That(first, Is.Not.SameAs(equal));
            Assert.That(first.Equals(equal), Is.True);
            untyped.OnUpdated(first, callback);
            Assert.Throws<InvalidOperationException>(() => untyped.OnUpdated(equal, callback));
            untyped.OnUpdated(first, plain);
            Assert.Throws<InvalidOperationException>(() => untyped.OnUpdated(equal, plain));
            builder.OnUpdated(first, callback);
            Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(EventCondition.Where(equal), callback));
            untyped.OnUpdated(_ => true, callback);

            hub.Update(0);
            hub.Update(1);
            Assert.That(receiver.Calls, Is.EqualTo(5));
        }

        [Test]
        public void DebugConditionsCompareEveryStepByKindMessageAndLogLevel()
        {
            using var hub = new EventHub<int>(new object());
            var builder = hub.Connect(new object());
            Action<int> callback = _ => { };
            Func<int, bool> predicate = value => value > 0;
            var condition = EventCondition.Where(predicate).When(1).Debug("same", LogLevel.Warning);
            builder.OnUpdated(condition, callback);

            Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(
                EventCondition.Where(predicate).When(1).Debug("same", LogLevel.Warning), callback));
            Assert.DoesNotThrow(() => builder.OnUpdated(
                EventCondition.Where(predicate).When(1).Debug("other", LogLevel.Warning), callback));
            Assert.DoesNotThrow(() => builder.OnUpdated(
                EventCondition.Where(predicate).When(1).Debug("same", LogLevel.Log), callback));
            Assert.DoesNotThrow(() => builder.OnUpdated(EventCondition.Where(predicate).When(1), callback));
            Assert.DoesNotThrow(() => builder.OnUpdated(EventCondition.When(1).Where(predicate), callback));
            Assert.DoesNotThrow(() => builder.OnUpdated(EventCondition.Where(predicate).Where(predicate), callback));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectUpdatedRemovalPreservesConditionalRegistrations(bool objectEvent)
        {
            using var hub = new EventHub<object>(new object());
            var received = new List<object>();
            Action<object> callback = received.Add;
            var builder = hub.Connect(callback);

            if (objectEvent)
            {
                var untyped = (IEventHubBuilder)builder;
                untyped.OnUpdated(callback).OnUpdated(value => (int)value > 0, callback);
                ((IEventHub)hub).Updated -= callback;
            }
            else
            {
                builder.OnUpdated(callback).OnUpdated(value => (int)value > 0, callback);
                hub.Updated -= callback;
            }

            hub.Update(0);
            hub.Update(1);
            Assert.That(received, Is.EqualTo(new object[] { 1 }));
            Assert.That(hub.HasListener(callback), Is.True);
        }

        [Test]
        public void DirectAndListenerCallbacksInterleaveInRegistrationOrder()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var first = hub.Connect(new object());
            var second = hub.Connect(new object());
            hub.Updated += _ => calls.Add("direct first");
            second.OnUpdated(_ => calls.Add("second listener"));
            ((IEventHub)hub).Updated += _ => calls.Add("object direct");
            first.OnUpdated(_ => calls.Add("first listener"));
            second.OnUpdated(_ => calls.Add("second tail"));

            hub.Update(1);

            Assert.That(calls, Is.EqualTo(new[]
            {
                "direct first", "second listener", "object direct", "first listener", "second tail",
            }));
            Assert.That(hub.ConnectionCount, Is.EqualTo(4));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StaleBuildersRejectEveryRegistrationAndDisposedHubsTakePrecedence(bool reconnect)
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var root = hub.Connect(listener);
            var registered = root.OnUpdated(EventCondition.Where<int>(_ => true).Debug(), () => { });
            hub.Disconnect(listener);
            var freshCalls = 0;

            if (reconnect) hub.Connect(listener).OnUpdated(() => freshCalls++);

            AssertBuilderUnavailable<InvalidOperationException>(root);
            AssertBuilderUnavailable<InvalidOperationException>(registered);
            hub.Update(1);
            Assert.That(freshCalls, Is.EqualTo(reconnect ? 1 : 0));
            hub.Dispose();
            AssertBuilderUnavailable<ObjectDisposedException>(root);
            AssertBuilderUnavailable<ObjectDisposedException>(registered);
        }

        [Test]
        public void DisposedHubRejectsEveryMethodOnItsPreviouslyConnectedBuilder()
        {
            var hub = new EventHub<int>(new object());
            var builder = hub.Connect(new object());
            hub.Dispose();

            AssertBuilderUnavailable<ObjectDisposedException>(builder);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void DisconnectSkipsMulticastTailsOnEveryBuilderPath(bool objectBuilder, bool parameterless)
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var builder = hub.Connect(listener);
            var tails = 0;
            Action disconnect = () => hub.Disconnect(listener);
            Action tail = () => tails++;

            if (objectBuilder)
            {
                var untyped = (IEventHubBuilder)builder;
                if (parameterless) untyped.OnUpdated(_ => true, disconnect + tail);
                else untyped.OnUpdated(_ => true, new Action<object>(_ => disconnect()) + (_ => tail()));
            }
            else if (parameterless) builder.OnUpdated(_ => true, disconnect + tail);
            else builder.OnUpdated(_ => true, new Action<int>(_ => disconnect()) + (_ => tail()));

            hub.Update(1);

            Assert.That(tails, Is.Zero);
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        // Update failures
        [TestCase(false)]
        [TestCase(true)]
        public void FailedListenerCallbackLeavesItsOtherCallbacksAndLifecycleConnected(bool parameterless)
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var reports = new List<Exception>();
            var values = new List<int>();
            var failure = new ApplicationException("listener");
            var disposed = 0;
            ExceptionHandler.SetHandler(reports.Add);
            var builder = hub.Connect(listener);
            var condition = EventCondition.Where<int>(_ => true);

            if (parameterless)
                builder.OnUpdated(condition, new Action(() => throw failure) + (() => values.Add(9)));
            else
                builder.OnUpdated(condition, new Action<int>(_ => throw failure) + values.Add);

            builder.OnUpdated(condition, value => values.Add(-value)).OnDisposed(() => disposed++);
            hub.Update(1);
            hub.Update(2);

            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(hub.HasListener(listener), Is.True);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            Assert.That(values, Is.EqualTo(new[] { -1, -2 }));
            hub.Dispose();
            Assert.That(disposed, Is.EqualTo(1));
        }

        [TestCase(UpdateEntry.Typed)]
        [TestCase(UpdateEntry.Object)]
        [TestCase(UpdateEntry.Parameterless)]
        public void LifecycleConnectedListenerCanReregisterAFailedCallbackOnTheSamePathDuringUpdate(UpdateEntry entry)
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var reports = new List<Exception>();
            var failure = new ApplicationException("retry with lifecycle");
            var attempts = 0;
            var disposed = 0;
            var registeredAgain = false;
            var connectedDuringRetry = false;
            Action receive = () =>
            {
                attempts++;
                if (attempts == 1) throw failure;
            };

            Delegate callback = entry switch
            {
                UpdateEntry.Typed => new Action<int>(_ => receive()),
                UpdateEntry.Object => new Action<object>(_ => receive()),
                _ => receive,
            };

            ExceptionHandler.SetHandler(reports.Add);
            var builder = hub.Connect(listener).OnDisposed(() => disposed++);
            RegisterConditionalUpdate(builder, entry, callback);

            hub.Connect(new object()).OnUpdated(value => value > 0, value =>
            {
                if (value != 1) return;

                connectedDuringRetry = hub.HasListener(listener);
                RegisterConditionalUpdate(builder, entry, callback);
                registeredAgain = true;
            });

            hub.Update(1);

            Assert.That(connectedDuringRetry, Is.True);
            Assert.That(registeredAgain, Is.True);
            Assert.That(attempts, Is.EqualTo(1));
            hub.Update(2);

            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(hub.HasListener(listener), Is.True);
            hub.Dispose();
            Assert.That(disposed, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnhandledUpdateFailuresFinishTraversalAndRethrowTheFirstException(bool throwingReporter)
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var first = new ApplicationException("first");
            var second = new ApplicationException("second");
            var reportFailure = new InvalidOperationException("reporter");
            var reports = new List<Exception>();
            var calls = new List<int>();

            if (throwingReporter)
                ExceptionHandler.SetHandler(exception => { reports.Add(exception); throw reportFailure; });

            hub.Connect(listener).OnUpdated(_ => throw first).OnUpdated(calls.Add);
            hub.Updated += _ => throw second;
            hub.Updated += value => calls.Add(-value);

            var actual = Assert.Catch<Exception>(() => hub.Update(1));
            Assert.That(actual, Is.SameAs(throwingReporter ? reportFailure : first));
            Assert.That(calls, Is.EqualTo(new[] { 1, -1 }));
            Assert.That(hub.HasListener(listener), Is.True);
            hub.Update(2);
            Assert.That(calls, Is.EqualTo(new[] { 1, -1, 2, -2 }));
            Assert.That(reports, Is.EqualTo(throwingReporter ? new[] { first, second } : Array.Empty<Exception>()));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void FailedDirectSubscriptionsAreReleasedAndCanBeRemovedSafely(bool objectEvent, bool handled)
        {
            using var hub = new EventHub<int>(new object());
            var untyped = (IEventHub)hub;
            var failure = new ApplicationException("direct");
            var reports = new List<Exception>();
            var values = new List<int>();
            Action<int> typed = _ => throw failure;
            Action<object> adapted = _ => throw failure;

            if (handled) ExceptionHandler.SetHandler(reports.Add);
            if (objectEvent) untyped.Updated += adapted;
            else hub.Updated += typed;

            hub.Connect(new object()).OnUpdated(values.Add);

            if (handled) Assert.DoesNotThrow(() => hub.Update(1));
            else Assert.That(Assert.Throws<ApplicationException>(() => hub.Update(1)), Is.SameAs(failure));

            if (objectEvent) Assert.DoesNotThrow(() => untyped.Updated -= adapted);
            else Assert.DoesNotThrow(() => hub.Updated -= typed);

            hub.Update(2);
            Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(reports, Is.EqualTo(handled ? new[] { failure } : Array.Empty<Exception>()));
        }

        [Test]
        public void FailedWhereReleasesTheSharedPathAndDisconnectsEmptyListeners()
        {
            using var hub = new EventHub<int>(new object());
            var reports = new List<Exception>();
            var values = new List<int>();
            var failure = new ApplicationException("predicate");
            Func<int, bool> predicate = _ => throw failure;
            ExceptionHandler.SetHandler(reports.Add);
            var first = hub.Connect(new object());
            first.OnUpdated(predicate, _ => Assert.Fail("Failed path must not publish."));
            hub.Connect(new object()).OnUpdated(predicate, _ => Assert.Fail("Shared path must be released."));
            first.OnUpdated(values.Add);

            hub.Update(1);
            hub.Update(2);

            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void FailedWhereImmediatelyDisconnectsEmptyListenersAndKeepsLifecycleConnections()
        {
            using var hub = new EventHub<int>(new object());
            var retainedListener = new object();
            var removedListener = new object();
            var reports = new List<Exception>();
            var failure = new ApplicationException("shared predicate");
            var values = new List<int>();
            var disposed = 0;
            var retainedDuringUpdate = false;
            var removedDuringUpdate = true;
            var observed = false;
            Func<int, bool> predicate = value => value == 1 ? throw failure : true;
            ExceptionHandler.SetHandler(reports.Add);
            var retained = hub.Connect(retainedListener).OnDisposed(() => disposed++);
            Action<int> first = values.Add;
            Action<int> second = value => values.Add(-value);
            retained.OnUpdated(predicate, first).OnUpdated(predicate, second);
            var removed = hub.Connect(removedListener);
            removed.OnUpdated(predicate, first).OnUpdated(predicate, second);

            hub.Connect(new object()).OnUpdated(value => value > 0, value =>
            {
                if (value != 1) return;

                retainedDuringUpdate = hub.HasListener(retainedListener);
                removedDuringUpdate = hub.HasListener(removedListener);
                retained.OnUpdated(predicate, first).OnUpdated(predicate, second);
                observed = true;
            });

            hub.Update(1);

            Assert.That(observed, Is.True);
            Assert.That(retainedDuringUpdate, Is.True);
            Assert.That(removedDuringUpdate, Is.False);
            Assert.That(values, Is.Empty);
            AssertBuilderUnavailable<InvalidOperationException>(removed);
            hub.Update(2);

            Assert.That(values, Is.EqualTo(new[] { 2, -2 }));
            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(hub.ConnectionCount, Is.EqualTo(2));
            hub.Dispose();
            Assert.That(disposed, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedSubscriptionsDoNotRetainTheirCallbackTargets(bool direct)
        {
            using var hub = new EventHub<int>(new object());
            ExceptionHandler.SetHandler(_ => { });
            var target = RegisterFailingTarget(hub, direct);

            hub.Update(1);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.That(target.IsAlive, Is.False);
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [Test]
        public void LifecycleConnectionDoesNotRetainAFailedCallbackTarget()
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var disposed = 0;
            var builder = hub.Connect(listener).OnDisposed(() => disposed++);
            ExceptionHandler.SetHandler(_ => { });
            var target = RegisterFailingTarget(builder);

            hub.Update(1);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.That(target.IsAlive, Is.False);
            Assert.That(hub.HasListener(listener), Is.True);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            hub.Dispose();
            Assert.That(disposed, Is.EqualTo(1));
            GC.KeepAlive(builder);
        }

        // Sleep
        [TestCase(SleepEntry.Sleep)]
        [TestCase(SleepEntry.SleepWithToken)]
        [TestCase(SleepEntry.TrySleep)]
        [TestCase(SleepEntry.TrySleepWithResult)]
        public void SleepSkipsUpdatesUntilItsTokenAwakensTheHub(SleepEntry entry)
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            var awakenings = 0;
            hub.Connect(new object()).OnUpdated(values.Add);
            Assert.That(hub.IsAwake, Is.True);

            var token = StartSleep(hub, entry, () => awakenings++);
            hub.Update(1);
            Assert.That(hub.IsAwake, Is.False);
            Assert.That(values, Is.Empty);
            Assert.That(awakenings, Is.Zero);

            token.Dispose();
            Assert.That(hub.IsAwake, Is.True);
            Assert.That(awakenings, Is.EqualTo(1));
            Assert.That(values, Is.Empty);

            hub.Update(2);
            Assert.That(values, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void OnAwakeRunsAfterTheHubAwakensAndCanPublishAnUpdate()
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            hub.Connect(new object()).OnUpdated(values.Add);
            var token = hub.Sleep(() => hub.Update(1));

            hub.Update(0);
            token.Dispose();

            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void SleepingHubRejectsNestedSleepAndKeepsItsOriginalToken()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var token = hub.Sleep(() => calls.Add("outer"));

            Assert.Throws<InvalidOperationException>(() => hub.Sleep());
            Assert.Throws<InvalidOperationException>(() => hub.Sleep(new Token()));
            Assert.That(hub.TrySleep(out var rejected), Is.False);
            Assert.That(rejected, Is.Null);
            hub.TrySleep(() => calls.Add("nested")).Dispose();
            Assert.That(hub.IsAwake, Is.False);
            Assert.That(calls, Is.EqualTo(new[] { "nested" }));

            token.Dispose();
            Assert.That(hub.IsAwake, Is.True);
            Assert.That(calls, Is.EqualTo(new[] { "nested", "outer" }));
        }

        [TestCase(SleepEntry.Sleep)]
        [TestCase(SleepEntry.SleepWithToken)]
        [TestCase(SleepEntry.TrySleep)]
        [TestCase(SleepEntry.TrySleepWithResult)]
        public void DisposingTheHubIgnoresItsLaterAwakening(SleepEntry entry)
        {
            using var hub = new EventHub<int>(new object());
            var awakenings = 0;
            var token = StartSleep(hub, entry, () => awakenings++);

            hub.Dispose();
            token.Dispose();

            Assert.That(awakenings, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SleepRejectsAnInvalidTokenAndStaysAwake(bool disposedToken)
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            var awakenings = 0;
            hub.Connect(new object()).OnUpdated(values.Add);
            var token = disposedToken ? new Token() : null;
            token?.Dispose();

            var exception = Assert.Throws<ArgumentException>(() => hub.Sleep(token, () => awakenings++));
            hub.Update(1);

            Assert.That(exception.ParamName, Is.EqualTo("token"));
            Assert.That(hub.IsAwake, Is.True);
            Assert.That(awakenings, Is.Zero);
            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        // Disposal
        [Test]
        public void DisposeNotifiesListenersOnceAndIgnoresRepeatedCalls()
        {
            var hub = CreateLifecycleHub(out var dispose);
            var notifications = 0;
            hub.Connect(new object()).OnDisposed(() => notifications++);
            Assert.That(hub.IsDisposed, Is.False);

            dispose();
            Assert.That(hub.IsDisposed, Is.True);
            Assert.That(notifications, Is.EqualTo(1));

            Assert.DoesNotThrow(() => dispose());
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void DisposePassesTheOwnerToListenersAndReleasesEveryConnection()
        {
            var owner = new object();
            using var hub = new EventHub<int>(owner);
            var listener = new object();
            var owners = new List<object>();
            hub.Connect(listener).OnDisposing(owners.Add).OnDisposed(owners.Add);

            hub.Dispose();

            Assert.That(owners, Is.EqualTo(new[] { owner, owner }));
            Assert.That(hub.HasListener(listener), Is.False);
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [Test]
        public void DisposalTraversesRegisteredCallbacksWithoutAllocating()
        {
            PrepareDisposal(new Counter())();
            var counter = new Counter();
            var dispose = PrepareDisposal(counter);

            var bytes = MeasureDisposal(dispose);
            Assert.That(counter.Calls, Is.EqualTo(6));
            Assert.That(bytes, Is.Zero);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasureDisposal(Action dispose)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            dispose();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Test]
        public void DisposalSkipsRemovedDirectSubscriptionsAndDisconnectedListeners()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var listener = new object();
            var connected = hub.Connect(listener);

            connected.OnDisposing(() =>
            {
                calls.Add("listener disposing");
                hub.Disconnect(listener);
            }).OnDisposing(() => calls.Add("listener tail"))
                .OnDisposed(() => calls.Add("listener disposed"));

            Action removed = () => calls.Add("removed direct");

            hub.Disposing += () =>
            {
                calls.Add("direct disposing");
                hub.Disposing -= removed;
            };

            hub.Disposing += removed;
            hub.Disposed += () => calls.Add("direct disposed");

            hub.Dispose();

            Assert.That(calls, Is.EquivalentTo(new[] { "direct disposing", "listener disposing", "direct disposed" }));
        }

        [Test]
        public void DisposalFromUpdateWaitsForTheCurrentCallbackAndSkipsItsMulticastTail()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();

            Action<int> dispose = _ =>
            {
                calls.Add("before");
                hub.Dispose();
                calls.Add("after");
            };

            hub.Connect(new object()).OnUpdated(dispose + (_ => calls.Add("tail")));
            hub.Updated += _ => calls.Add("direct");
            hub.Disposing += () => calls.Add("disposing");
            hub.Disposed += () => calls.Add("disposed");

            hub.Update(0);

            Assert.That(hub.IsDisposed, Is.True);
            Assert.That(calls, Is.EqualTo(new[] { "before", "after", "disposing", "disposed" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SubscriptionDisposalNotifiesEveryLifecycleConnection(bool duringUpdate)
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var updates = 0;
            hub.Updated += _ => hub.Dispose();

            for (int index = 0; index < 3; index++)
            {
                var name = index.ToString();
                hub.Connect(new object()).OnUpdated(_ => updates++)
                    .OnUpdated(_ => updates += 2)
                    .OnDisposing(() => calls.Add($"disposing:{name}:{hub.IsDisposed}"))
                    .OnDisposed(() => calls.Add($"disposed:{name}:{hub.IsDisposed}"));
            }

            hub.Connect(new object()).OnUpdated(_ => updates++);

            if (duringUpdate) Assert.DoesNotThrow(() => hub.Update(1));
            else Assert.DoesNotThrow(hub.Dispose);

            Assert.That(calls.Count, Is.EqualTo(6));
            Assert.That(calls.GetRange(0, 3), Is.EquivalentTo(new[]
            {
                "disposing:0:False", "disposing:1:False", "disposing:2:False",
            }));
            Assert.That(calls.GetRange(3, 3), Is.EquivalentTo(new[]
            {
                "disposed:0:True", "disposed:1:True", "disposed:2:True",
            }));
            Assert.That(updates, Is.Zero);
            Assert.That(hub.IsDisposed, Is.True);
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [Test]
        public void DisposingASleepingHubReleasesItsTokenWithoutAwakening()
        {
            var counter = new Counter();
            using var token = new Token(counter.Tick);
            using var hub = new EventHub<int>(counter);
            hub.Sleep(token, counter.Tick);

            hub.Dispose();
            token.Dispose();

            Assert.That(counter.Calls, Is.EqualTo(1));
        }

        [Test]
        public void LifecycleCallbacksFromDifferentPathsKeepTheirNotificationOrderAndState()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var root = hub.Connect(new object());
            root.OnUpdated(_ => false, () => Assert.Fail("Condition must reject the value."))
                .OnDisposing(() => calls.Add($"listener disposing:{hub.IsDisposed}"));
            root.OnDisposed(() => calls.Add($"listener disposed:{hub.IsDisposed}"));
            hub.Disposing += () => calls.Add($"direct disposing:{hub.IsDisposed}");
            hub.Disposed += () => calls.Add($"direct disposed:{hub.IsDisposed}");

            hub.Dispose();

            Assert.That(calls.GetRange(0, 2), Is.EquivalentTo(new[]
            {
                "direct disposing:False", "listener disposing:False",
            }));
            Assert.That(calls.GetRange(2, 2), Is.EquivalentTo(new[]
            {
                "listener disposed:True", "direct disposed:True",
            }));
        }

        [Test]
        public void LifecycleFailureStillRemovesTheEntireListenerAndReportsOnce()
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var failure = new ApplicationException("disposing");
            var reports = new List<Exception>();
            var tail = 0;
            var direct = 0;
            ExceptionHandler.SetHandler(reports.Add);
            hub.Connect(listener).OnDisposing(() => throw failure)
                .OnDisposing(() => tail++).OnDisposed(() => tail++);
            hub.Disposed += () => direct++;

            hub.Dispose();

            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(tail, Is.Zero);
            Assert.That(direct, Is.EqualTo(1));
            Assert.That(hub.HasListener(listener), Is.False);
        }

        // Direct lifecycle subscriptions
        [TestCase(false)]
        [TestCase(true)]
        public void DirectLifecycleRejectsDuplicatesAndRemovesTheRegistration(bool disposing)
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            Action repeated = () => calls.Add("repeated");
            AddLifecycleCallback(hub, disposing, repeated);
            AddLifecycleCallback(hub, disposing, () => calls.Add("middle"));
            Assert.Throws<InvalidOperationException>(() => AddLifecycleCallback(hub, disposing, repeated));

            RemoveLifecycleCallback(hub, disposing, repeated);
            hub.Dispose();

            Assert.That(calls, Is.EqualTo(new[] { "middle" }));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void DirectLifecycleMulticastRemovalRequiresTheWholeRegistration(bool disposing, bool removeWhole)
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            Action first = () => calls.Add("first");
            Action second = () => calls.Add("second");
            var registration = first + second;
            AddLifecycleCallback(hub, disposing, registration);
            AddLifecycleCallback(hub, disposing, () => calls.Add("other"));

            RemoveLifecycleCallback(hub, disposing, removeWhole ? registration : second);
            hub.Dispose();

            Assert.That(calls, Is.EqualTo(removeWhole ? new[] { "other" } : new[] { "first", "second", "other" }));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void DirectLifecycleFailureSkipsItsMulticastTail(bool disposing, bool handled)
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var failure = new ApplicationException("lifecycle");
            var reports = new List<Exception>();
            if (handled) ExceptionHandler.SetHandler(reports.Add);

            Action bad = () =>
            {
                calls.Add("bad");
                throw failure;
            };

            AddLifecycleCallback(hub, disposing, bad + (() => calls.Add("tail")));
            AddLifecycleCallback(hub, disposing, () => calls.Add("other"));

            if (handled) hub.Dispose();
            else Assert.That(Assert.Throws<ApplicationException>(hub.Dispose), Is.SameAs(failure));

            Assert.That(calls, Is.EqualTo(handled ? new[] { "bad", "other" } : new[] { "bad" }));
            Assert.That(reports, Is.EqualTo(handled ? new[] { failure } : Array.Empty<Exception>()));
            Assert.That(hub.IsDisposed, Is.True);
            Assert.That(hub.ConnectionCount, Is.Zero);
            Assert.DoesNotThrow(hub.Dispose);
        }

        // Unified registrations
        [Test]
        public void ConstructorCallbacksShareAConnectionAndRemoveOnlyTheirOwnPhase()
        {
            var calls = 0;
            Action callback = () => calls++;
            using var hub = new EventHub<int>(new object(), callback, callback);

            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            Assert.That(hub.HasListener(callback), Is.True);
            Assert.Throws<InvalidOperationException>(() => hub.Connect(callback));
            Assert.Throws<InvalidOperationException>(() => hub.Disposing += callback);
            hub.Disposing -= callback;
            Assert.That(hub.HasListener(callback), Is.True);
            hub.Dispose();

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void DirectEventsAndBuilderRegistrationsShareTheirDelegateKey()
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            Action<int> callback = values.Add;
            var builder = hub.Connect(callback).OnUpdated(callback);
            var disposed = 0;
            builder.OnDisposed(() => disposed++);

            Assert.Throws<InvalidOperationException>(() => hub.Updated += callback);
            hub.Updated -= callback;
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            hub.Updated += callback;
            Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(callback));
            hub.Update(1);
            hub.Disconnect(callback);
            hub.Update(2);
            hub.Dispose();

            Assert.That(values, Is.EqualTo(new[] { 1 }));
            Assert.That(disposed, Is.Zero);
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TypedAndObjectUpdatedKeepSeparateRegistrations(bool throughBuilder)
        {
            using var hub = new EventHub<object>(new object());
            var values = new List<object>();
            Action<object> callback = values.Add;
            var untyped = (IEventHub)hub;

            if (throughBuilder)
            {
                var builder = hub.Connect(callback).OnUpdated(callback);
                ((IEventHubBuilder)builder).OnUpdated(callback);
                Assert.Throws<InvalidOperationException>(() => builder.OnUpdated(callback));
                Assert.Throws<InvalidOperationException>(() => ((IEventHubBuilder)builder).OnUpdated(callback));
            }
            else
            {
                hub.Updated += callback;
                untyped.Updated += callback;
            }

            Assert.Throws<InvalidOperationException>(() => hub.Updated += callback);
            Assert.Throws<InvalidOperationException>(() => untyped.Updated += callback);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            hub.Update("both");
            hub.Updated -= callback;
            hub.Updated -= callback;
            hub.Update("object");
            Assert.That(hub.HasListener(callback), Is.True);
            untyped.Updated -= callback;
            hub.Update("neither");

            Assert.That(values, Is.EqualTo(new object[] { "both", "both", "object" }));
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [TestCase(UpdateEntry.Typed)]
        [TestCase(UpdateEntry.Object)]
        [TestCase(UpdateEntry.Parameterless)]
        public void BuilderRejectsDuplicatesByConditionValue(UpdateEntry entry)
        {
            using var hub = new EventHub<int>(new object());
            var calls = 0;
            Delegate callback = entry switch
            {
                UpdateEntry.Typed => new Action<int>(_ => calls++),
                UpdateEntry.Object => new Action<object>(_ => calls++),
                _ => new Action(() => calls++),
            };

            var root = hub.Connect(new object());
            RegisterUpdate(root, entry, callback);
            RegisterConditionalUpdate(root, entry, callback);
            Assert.Throws<InvalidOperationException>(() => RegisterUpdate(root, entry, callback));
            Assert.Throws<InvalidOperationException>(() => RegisterConditionalUpdate(root, entry, callback));
            RegisterConditionalUpdate(root, entry, callback, true);
            RegisterUpdate(hub.Connect(new object()), entry, callback);
            hub.Update(1);

            Assert.That(calls, Is.EqualTo(4));
            Assert.That(hub.ConnectionCount, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LifecycleOverloadsShareRegistrationOrderAndIgnorePaths(bool disposing)
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            var root = hub.Connect(new object());
            var path = root.OnUpdated(_ => false, () => { });
            Action plain = () => calls.Add("plain");
            Action<object> owner = _ => calls.Add("owner");

            if (disposing)
            {
                root.OnDisposing(owner).OnDisposing(plain).OnDisposing(_ => calls.Add("tail"));
                Assert.Throws<InvalidOperationException>(() => path.OnDisposing(plain));
                Assert.Throws<InvalidOperationException>(() => path.OnDisposing(owner));
            }
            else
            {
                root.OnDisposed(owner).OnDisposed(plain).OnDisposed(_ => calls.Add("tail"));
                Assert.Throws<InvalidOperationException>(() => path.OnDisposed(plain));
                Assert.Throws<InvalidOperationException>(() => path.OnDisposed(owner));
            }

            hub.Dispose();
            Assert.That(calls, Is.EqualTo(new[] { "owner", "plain", "tail" }));
        }

        [Test]
        public void RemovingCurrentLifecycleRegistrationSkipsItsTailAndKeepsOtherRegistrations()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            Action registration = null;
            registration = new Action(() =>
            {
                calls.Add("remove");
                hub.Disposing -= registration;
            }) + (() => calls.Add("tail"));

            hub.Connect(registration).OnDisposing(registration)
                .OnDisposing(() => calls.Add("next"))
                .OnDisposed(() => calls.Add("disposed"));
            hub.Dispose();

            Assert.That(calls, Is.EqualTo(new[] { "remove", "next", "disposed" }));
        }

        [Test]
        public void DirectLifecycleFailureDisconnectsAllRegistrationsWithTheSameKey()
        {
            using var hub = new EventHub<int>(new object());
            var reports = new List<Exception>();
            var failure = new ApplicationException("direct lifecycle");
            Action callback = () => throw failure;
            var tail = 0;
            ExceptionHandler.SetHandler(reports.Add);
            hub.Connect(callback).OnDisposed(() => tail++);
            hub.Disposing += callback;
            hub.Disposed += callback;
            hub.Dispose();

            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(tail, Is.Zero);
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectMulticastFailureRemovesTheWholeRegistration(bool objectEvent)
        {
            using var hub = new EventHub<int>(new object());
            var reports = new List<Exception>();
            var failure = new ApplicationException("multicast");
            var tails = 0;
            ExceptionHandler.SetHandler(reports.Add);
            Action<int> typed = new Action<int>(_ => throw failure) + (_ => tails++);
            Action<object> untyped = new Action<object>(_ => throw failure) + (_ => tails++);
            if (objectEvent) ((IEventHub)hub).Updated += untyped;
            else hub.Updated += typed;

            hub.Update(1);
            hub.Update(2);

            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(tails, Is.Zero);
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingCurrentDirectUpdateSkipsItsMulticastTail(bool objectEvent)
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            Action<int> typed = null;
            Action<object> untyped = null;
            typed = new Action<int>(_ => { calls.Add("remove"); hub.Updated -= typed; })
                + (_ => calls.Add("tail"));
            untyped = new Action<object>(_ => { calls.Add("remove"); ((IEventHub)hub).Updated -= untyped; })
                + (_ => calls.Add("tail"));
            if (objectEvent) ((IEventHub)hub).Updated += untyped;
            else hub.Updated += typed;

            hub.Update(1);
            hub.Update(2);

            Assert.That(calls, Is.EqualTo(new[] { "remove" }));
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LastUpdateFailureDisconnectsItsBuilderEvenWhenReportingThrows(bool handled)
        {
            using var hub = new EventHub<int>(new object());
            var listener = new object();
            var failure = new ApplicationException("last");
            var builder = hub.Connect(listener).OnUpdated(_ => throw failure);
            var emptyListener = new object();
            var emptyBuilder = hub.Connect(emptyListener);
            if (handled) ExceptionHandler.SetHandler(_ => { });

            if (handled) hub.Update(1);
            else Assert.Throws<ApplicationException>(() => hub.Update(1));

            Assert.That(hub.HasListener(listener), Is.False);
            Assert.That(hub.HasListener(emptyListener), Is.True);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            AssertBuilderUnavailable<InvalidOperationException>(builder);
            var calls = 0;
            hub.Connect(listener).OnUpdated(_ => calls++);
            emptyBuilder.OnUpdated(_ => calls++);
            hub.Update(2);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void LastDirectRemovalInvalidatesTheSharedBuilderAndAllowsReconnection()
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            Action<int> callback = values.Add;
            var old = hub.Connect(callback).OnUpdated(callback);
            hub.Updated -= callback;
            Assert.That(hub.HasListener(callback), Is.False);
            var fresh = hub.Connect(callback).OnUpdated(callback);
            AssertBuilderUnavailable<InvalidOperationException>(old);
            hub.Update(1);
            hub.Updated -= callback;
            AssertBuilderUnavailable<InvalidOperationException>(fresh);
            Assert.That(values, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void NullCallbacksDoNotCreateConnectionsAndLifetimeChecksStillRunFirst()
        {
            using var hub = new EventHub<int>(new object());
            hub.Updated += null;
            ((IEventHub)hub).Updated += null;
            hub.Disposing += null;
            hub.Disposed += null;
            Assert.That(hub.ConnectionCount, Is.Zero);
            var builder = hub.Connect(new object());
            builder.OnUpdated((Action<int>)null).OnUpdated((Action)null)
                .OnDisposing((Action)null).OnDisposing((Action<object>)null)
                .OnDisposed((Action)null).OnDisposed((Action<object>)null);
            Func<int, bool> predicate = _ => throw new ApplicationException("No subscription should be created.");
            var condition = EventCondition.Where(predicate);
            Assert.That(builder.OnUpdated(1, (Action<int>)null), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(1, (Action)null), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(predicate, (Action<int>)null), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(predicate, (Action)null), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(condition, (Action<int>)null), Is.SameAs(builder));
            Assert.That(builder.OnUpdated(condition, (Action)null), Is.SameAs(builder));
            var untyped = (IEventHubBuilder)builder;
            untyped.OnUpdated((Action<object>)null);
            Func<object, bool> objectPredicate = _ => throw new ApplicationException("No object subscription.");
            Assert.That(untyped.OnUpdated(objectPredicate, (Action<object>)null), Is.SameAs(builder));
            Assert.That(untyped.OnUpdated(objectPredicate, (Action)null), Is.SameAs(builder));
            hub.Update(1);
            Assert.That(hub.ConnectionCount, Is.EqualTo(1));
            hub.Dispose();
            Assert.Throws<ObjectDisposedException>(() => hub.Updated += null);
            Assert.Throws<ObjectDisposedException>(() => ((IEventHub)hub).Updated += null);
            Assert.Throws<ObjectDisposedException>(() => hub.Disposing += null);
            Assert.Throws<ObjectDisposedException>(() => hub.Disposed += null);
            Assert.DoesNotThrow(() => hub.Updated -= null);
            Assert.DoesNotThrow(() => ((IEventHub)hub).Updated -= null);
            Assert.DoesNotThrow(() => hub.Disposing -= null);
            Assert.DoesNotThrow(() => hub.Disposed -= null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LifecycleFailureAfterSelfRemovalStillReportsTheOriginalException(bool disconnect)
        {
            using var hub = new EventHub<int>(new object());
            var failure = new ApplicationException("after removal");
            var reports = new List<Exception>();
            ExceptionHandler.SetHandler(reports.Add);
            Action callback = null;
            callback = () =>
            {
                if (disconnect) hub.Disconnect(callback);
                else hub.Disposing -= callback;

                throw failure;
            };

            hub.Disposing += callback;
            Assert.DoesNotThrow(hub.Dispose);
            Assert.That(reports, Is.EqualTo(new[] { failure }));
            Assert.That(hub.ConnectionCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedUpdateCanBeRegisteredAgainDuringTheSameUpdate(bool direct)
        {
            using var hub = new EventHub<int>(new object());
            var values = new List<int>();
            var reports = new List<Exception>();
            Exception registrationFailure = null;
            var failure = new ApplicationException("retry");
            Action<int> callback = value =>
            {
                if (value == 1) throw failure;
                values.Add(value);
            };

            ExceptionHandler.SetHandler(reports.Add);
            var builder = direct ? null : hub.Connect(new object());
            if (direct) hub.Updated += callback;
            else builder.OnUpdated(callback);

            hub.Updated += value =>
            {
                if (value != 1) return;
                if (direct) hub.Updated += callback;
                else
                {
                    try
                    {
                        builder.OnUpdated(callback);
                    }
                    catch (Exception exception)
                    {
                        registrationFailure = exception;
                    }
                }
            };

            hub.Update(1);
            Assert.That(values, Is.Empty);
            if (!direct) Assert.That(registrationFailure, Is.TypeOf<InvalidOperationException>());

            hub.Update(2);
            Assert.That(values, Is.EqualTo(direct ? new[] { 2 } : Array.Empty<int>()));
            Assert.That(reports, Is.EqualTo(new[] { failure }));
        }

        [Test]
        public void StaleBuilderDoesNotRetainARemovedDelegateKey()
        {
            using var hub = new EventHub<int>(new object());
            var target = RegisterAndRemoveDelegateKey(hub, out var stale);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.That(target.IsAlive, Is.False);
            AssertBuilderUnavailable<InvalidOperationException>(stale);
            GC.KeepAlive(stale);
        }

        [Test]
        public void RemovingALaterLifecycleRegistrationKeepsTheCurrentAndFollowingCallbacks()
        {
            using var hub = new EventHub<int>(new object());
            var calls = new List<string>();
            Action removed = () => calls.Add("removed");
            hub.Connect(removed).OnDisposing(new Action(() =>
            {
                calls.Add("remove");
                hub.Disposing -= removed;
            }) + (() => calls.Add("current tail")))
                .OnDisposing(removed).OnDisposing(() => calls.Add("next"));

            hub.Dispose();
            Assert.That(calls, Is.EqualTo(new[] { "remove", "current tail", "next" }));
        }

        // Helpers
        private static bool IsPositive(int value) => value > 0;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RegisterAndRemoveDelegateKey(EventHub<int> hub, out IEventHubBuilder<int> builder)
        {
            var target = new FailingReceiver();
            Action<int> callback = target.Receive;
            builder = hub.Connect(callback).OnUpdated(callback);
            hub.Updated -= callback;
            return new WeakReference(target);
        }

        private static void RegisterUpdate(IEventHubBuilder<int> builder, UpdateEntry entry, Delegate callback)
        {
            switch (entry)
            {
                case UpdateEntry.Typed:
                    builder.OnUpdated((Action<int>)callback);
                    break;
                case UpdateEntry.Object:
                    ((IEventHubBuilder)builder).OnUpdated((Action<object>)callback);
                    break;
                default:
                    builder.OnUpdated((Action)callback);
                    break;
            }
        }

        private static void RegisterConditionalUpdate(
            IEventHubBuilder<int> builder,
            UpdateEntry entry,
            Delegate callback,
            bool differentCondition = false)
        {
            Func<int, bool> predicate = differentCondition ? static value => value != 0 : static value => value > 0;
            Func<object, bool> objectPredicate
                = differentCondition ? static value => (int)value != 0 : static value => (int)value > 0;

            switch (entry)
            {
                case UpdateEntry.Typed:
                    builder.OnUpdated(EventCondition.Where(predicate), (Action<int>)callback);
                    break;
                case UpdateEntry.Object:
                    ((IEventHubBuilder)builder).OnUpdated(objectPredicate, (Action<object>)callback);
                    break;
                default:
                    builder.OnUpdated(EventCondition.Where(predicate), (Action)callback);
                    break;
            }
        }

        public enum UpdateEntry { Typed, Object, Parameterless }

        private static void AddLifecycleCallback(EventHub<int> hub, bool disposing, Action callback)
        {
            if (disposing) hub.Disposing += callback;
            else hub.Disposed += callback;
        }

        private static void RemoveLifecycleCallback(EventHub<int> hub, bool disposing, Action callback)
        {
            if (disposing) hub.Disposing -= callback;
            else hub.Disposed -= callback;
        }

        private static void AssertBuilderUnavailable<TException>(IEventHubBuilder<int> builder)
            where TException : Exception
        {
            var untyped = (IEventHubBuilder)builder;
            var attempts = new TestDelegate[]
            {
                () => builder.OnUpdated(_ => { }),
                () => builder.OnUpdated(() => { }),
                () => builder.OnUpdated((Action<int>)null),
                () => builder.OnUpdated(1, _ => { }),
                () => builder.OnUpdated(1, () => { }),
                () => builder.OnUpdated(1, (Action<int>)null),
                () => builder.OnUpdated(1, (Action)null),
                () => builder.OnUpdated(_ => true, _ => { }),
                () => builder.OnUpdated(_ => true, () => { }),
                () => builder.OnUpdated((Func<int, bool>)null, _ => { }),
                () => builder.OnUpdated((Func<int, bool>)null, () => { }),
                () => builder.OnUpdated((Func<int, bool>)null, (Action<int>)null),
                () => builder.OnUpdated((Func<int, bool>)null, (Action)null),
                () => builder.OnUpdated(EventCondition.When(1), _ => { }),
                () => builder.OnUpdated(EventCondition.When(1), () => { }),
                () => builder.OnUpdated((EventCondition<int>)null, _ => { }),
                () => builder.OnUpdated((EventCondition<int>)null, () => { }),
                () => builder.OnUpdated((EventCondition<int>)null, (Action<int>)null),
                () => builder.OnUpdated((EventCondition<int>)null, (Action)null),
                () => builder.OnDisposing(() => { }),
                () => builder.OnDisposing(_ => { }),
                () => builder.OnDisposed(() => { }),
                () => builder.OnDisposed(_ => { }),
                () => untyped.OnUpdated(_ => { }),
                () => untyped.OnUpdated(() => { }),
                () => untyped.OnUpdated((Action<object>)null),
                () => untyped.OnUpdated(_ => true, _ => { }),
                () => untyped.OnUpdated(_ => true, () => { }),
                () => untyped.OnUpdated(null, _ => { }),
                () => untyped.OnUpdated(null, () => { }),
                () => untyped.OnUpdated(null, (Action<object>)null),
                () => untyped.OnUpdated(null, (Action)null),
                () => untyped.OnDisposing(() => { }),
                () => untyped.OnDisposing(_ => { }),
                () => untyped.OnDisposed(() => { }),
                () => untyped.OnDisposed(_ => { }),
                () => ((ILifecycleHubBuilder)builder).OnDisposed(() => { }),
            };

            foreach (var attempt in attempts)
                Assert.Throws<TException>(attempt);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RegisterFailingTarget(EventHub<int> hub, bool direct)
        {
            var target = new FailingReceiver();
            if (direct) hub.Updated += target.Receive;
            else hub.Connect(new object()).OnUpdated(_ => true, target.Receive);

            return new WeakReference(target);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RegisterFailingTarget(IEventHubBuilder<int> builder)
        {
            var target = new FailingReceiver();
            builder.OnUpdated(target.Receive);
            return new WeakReference(target);
        }

        private sealed class FailingReceiver
        {
            public void Receive(int value) => throw new ApplicationException("released target");
        }

        private static ILifecycleHub CreateLifecycleHub(out Action dispose)
        {
            var hub = new EventHub<int>(new object());
            dispose = hub.Dispose;
            return hub;
        }

        private static IDisposable StartSleep(EventHub<int> hub, SleepEntry entry, Action onAwake)
        {
            switch (entry)
            {
                case SleepEntry.Sleep:
                    return hub.Sleep(onAwake);
                case SleepEntry.SleepWithToken:
                    var token = new Token();
                    Assert.That(hub.Sleep(token, onAwake), Is.SameAs(token));
                    return token;
                case SleepEntry.TrySleep:
                    return hub.TrySleep(onAwake);
                case SleepEntry.TrySleepWithResult:
                    Assert.That(hub.TrySleep(out var result, onAwake), Is.True);
                    return result;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry), entry, null);
            }
        }

        private static EventHub<int> CreateUpdateHub(bool subscribe, Counter counter)
        {
            var hub = new EventHub<int>(counter);
            if (!subscribe) return hub;

            Action<int> typed = counter.Record;
            Action plain = counter.Tick;
            var builder = hub.Connect(counter).OnUpdated(typed + typed);
            builder.OnUpdated(static value => value == 1, typed);
            builder.OnUpdated(static value => value > 0, plain + plain);
            hub.Updated += typed;
            return hub;
        }

        private static Action PrepareDisposal(Counter counter)
        {
            var hub = new EventHub<int>(counter, counter.Tick, counter.Tick);
            hub.Connect(counter).OnUpdated(counter.Record)
                .OnDisposing(counter.Tick).OnDisposing(counter.RecordOwner)
                .OnDisposed(counter.Tick).OnDisposed(counter.RecordOwner);
            hub.Updated += counter.Record;
            return hub.Dispose;
        }

        public enum SleepEntry
        {
            Sleep,
            SleepWithToken,
            TrySleep,
            TrySleepWithResult,
        }

        private sealed class Counter
        {
            public int Calls;

            public void Record(int value) => Calls += value;
            public void Tick() => Calls++;
            public void RecordOwner(object owner)
            {
                if (ReferenceEquals(owner, this)) Calls++;
            }
        }

        private sealed class EqualityProbe : IEquatable<EqualityProbe>
        {
            // Internal
            private readonly int _value;
            private readonly Counter _counter;


            // Content
            public EqualityProbe(int value, Counter counter)
            {
                _value = value;
                _counter = counter;
            }

            public bool Equals(EqualityProbe other)
            {
                _counter.Calls++;
                return other != null && _value == other._value;
            }

            public override bool Equals(object obj) => obj is EqualityProbe other && Equals(other);
            public override int GetHashCode() => _value;
        }

        private sealed class ObjectConditionReceiver
        {
            public int Calls;

            public bool Accept(object value) => (int)value > 0;
            public void Receive(object value) => Calls++;
            public void Tick() => Calls++;
        }
    }
}
