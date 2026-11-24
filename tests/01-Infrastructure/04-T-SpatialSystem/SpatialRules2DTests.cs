using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Infrastructure;
using BlackThunder.UniEngine.Objects;
using BlackThunder.UniEngine.SpatialSystem;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture, Category("SpatialSystem")]
#if !UNITY_2022_3_OR_NEWER
    [NonParallelizable]
#endif
    public class SpatialRules2DTests
    {
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        [Test]
        public void FinalSettingsApplyAtUpdateAndCallbackChangesWaitForNextUpdate()
        {
            using var fixture = new SpatialFixture2D();
            var events = new List<string>();
            fixture.Space.CollisionEnter += _ =>
            {
                events.Add("enter");
                fixture.First.Enabled = false;
                Assert.That(fixture.FirstState.Enabled.Value, Is.True);
            };
            fixture.Space.CollisionExit += _ => events.Add("exit");
            fixture.First.Enabled = false;
            fixture.First.Enabled = true;
            fixture.OnUpdate = _ => fixture.Send(true);
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "enter" }));
            Assert.That(fixture.FirstState.Enabled.Value, Is.True);
            fixture.OnUpdate = null;
            fixture.Space.Update(0.1f);
            Assert.That(fixture.FirstState.Enabled.Value, Is.False);
            Assert.That(events, Is.EqualTo(new[] { "enter", "exit" }));
            fixture.Space.Update(0.1f);
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(fixture.QueryCount, Is.Zero);
        }

        [Test]
        public void PreviousExitCannotEndNewRelationshipAndReactivationDoesNotInventEnter()
        {
            using var fixture = new SpatialFixture2D();
            var pair = new CollisionPair2D(fixture.First, fixture.Second);
            var events = new List<string>();
            fixture.Space.CollisionEnter += _ => events.Add("enter");
            fixture.Space.CollisionExit += _ => events.Add("exit");
            fixture.Notify(new CollisionNotification2D(pair, false, true, 10));
            Assert.That(events, Is.Empty);
            fixture.Space.Update(0.1f);
            fixture.First.Enabled = false;
            fixture.Space.Update(0.1f);
            fixture.First.Enabled = true;
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "enter", "exit" }));
            fixture.Notify(new CollisionNotification2D(pair, false, true, 20));
            fixture.Notify(new CollisionNotification2D(pair, false, false, 10));
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "enter", "exit", "enter" }));
            fixture.Notify(new CollisionNotification2D(pair, false, false, 20));
            fixture.Notify(new CollisionNotification2D(pair, false, false, 20));
            fixture.Notify(new CollisionNotification2D(pair, false, true, 30));
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "enter", "exit", "enter", "exit", "enter" }));
        }

        [Test]
        public void TriggerChangesEndOnlyTheChangedKindAndNeedNativeConfirmation()
        {
            using var fixture = new SpatialFixture2D();
            var events = new List<string>();
            fixture.Space.CollisionEnter += _ => events.Add("contact enter");
            fixture.Space.CollisionExit += _ => events.Add("contact exit");
            fixture.Space.TriggerEnter += _ => events.Add("trigger enter");
            fixture.Space.TriggerExit += _ => events.Add("trigger exit");
            fixture.Send(true);
            fixture.Space.Update(0.1f);
            fixture.First.IsTrigger = true;
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "contact enter", "contact exit" }));
            fixture.Send(true, true);
            fixture.Space.Update(0.1f);
            fixture.Second.IsTrigger = true;
            fixture.Space.Update(0.1f);
            fixture.First.IsTrigger = false;
            fixture.Space.Update(0.1f);
            Assert.That(events.Count, Is.EqualTo(3));
            fixture.Second.IsTrigger = false;
            fixture.Space.Update(0.1f);
            Assert.That(events[3], Is.EqualTo("trigger exit"));
        }

        [Test]
        public void NestedNativeDeliveryWaitsUntilBothColliderCallbacksComplete()
        {
            using var fixture = new SpatialFixture2D();
            var order = new List<string>();
            fixture.Space.CollisionEnter += _ => { order.Add("world enter"); fixture.Send(false); };
            fixture.First.CollisionEnter += _ => order.Add("first enter");
            fixture.Second.CollisionEnter += _ => order.Add("second enter");
            fixture.Space.CollisionExit += _ => order.Add("world exit");
            fixture.First.CollisionExit += _ => order.Add("first exit");
            fixture.Second.CollisionExit += _ => order.Add("second exit");
            fixture.Send(true);
            fixture.Space.Update(0.1f);
            Assert.That(order, Is.EqualTo(new[]
            {
                "world enter", "first enter", "second enter", "world exit", "first exit", "second exit",
            }));
        }

        [Test]
        public void EachColliderSettlesItsOwnRelationsAndLeavesNothingBehind()
        {
            using var fixture = new SpatialFixture2D();
            var states = new List<SpatialFixture2D.ColliderState>();
            var others = new List<Collider2D>();
            var entered = new List<Collider2D>();
            var exited = new List<Collider2D>();
            fixture.First.CollisionEnter += pair => entered.Add(pair.Other(fixture.First));
            fixture.First.CollisionExit += pair => exited.Add(pair.Other(fixture.First));
            for (var index = 0; index < 5; index++)
            {
                states.Add(new SpatialFixture2D.ColliderState());
                others.Add(new Collider2D(fixture.Space, fixture.Bindings(states[index])));
                fixture.Notify(new CollisionNotification2D(
                    index % 2 == 0
                        ? new CollisionPair2D(fixture.First, others[index])
                        : new CollisionPair2D(others[index], fixture.First),
                    false, true, index + 1));
            }
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(others[3], others[4]), false, true, 10));
            fixture.Space.Update(0.1f);
            Assert.That(entered, Is.EqualTo(others));

            // Disposal and external destruction end a relation without an exit; a native exit ends only its own pair.
            others[0].Dispose();
            states[1].Alive = false;
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(others[2], fixture.First), false, false, 3));
            fixture.Space.Update(0.1f);
            Assert.That(others[1].IsDisposed, Is.True);
            Assert.That(exited, Is.EqualTo(new[] { others[2] }));

            fixture.First.Enabled = false;
            fixture.Space.Update(0.1f);
            Assert.That(exited, Is.EquivalentTo(new[] { others[2], others[3], others[4] }));

            // The pair between the other colliders never involved the disabled one.
            var unrelated = 0;
            others[3].CollisionExit += _ => unrelated++;
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(others[4], others[3]), false, false, 10));
            fixture.Space.Update(0.1f);
            Assert.That(unrelated, Is.EqualTo(1));

            fixture.First.Enabled = true;
            fixture.Space.Update(0.1f);
            var pair = new CollisionPair2D(fixture.First, others[3]);
            fixture.Notify(new CollisionNotification2D(pair, false, true, 20));
            fixture.Notify(new CollisionNotification2D(pair, false, false, 4));
            fixture.Space.Update(0.1f);
            Assert.That(entered.Count, Is.EqualTo(6));
            Assert.That(exited.Count, Is.EqualTo(3));
            fixture.Notify(new CollisionNotification2D(pair, false, false, 20));
            fixture.Space.Update(0.1f);
            Assert.That(exited.Count, Is.EqualTo(4));
        }

        [Test]
        public void TriggerChangeThatKeepsThePairKindNeitherDropsTheStartNorEndsTheRelation([Values] bool reverse)
        {
            using var fixture = new SpatialFixture2D();
            var pair = reverse
                ? new CollisionPair2D(fixture.Second, fixture.First)
                : new CollisionPair2D(fixture.First, fixture.Second);
            var events = new List<string>();
            fixture.Space.TriggerEnter += _ => events.Add("enter");
            fixture.Space.TriggerExit += _ => events.Add("exit");
            fixture.First.IsTrigger = true;
            fixture.Space.Update(0.1f);
            fixture.Notify(new CollisionNotification2D(pair, true, true, 1));
            fixture.Second.IsTrigger = true;
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "enter" }));

            // The second collider now keeps the kind alone.
            fixture.First.IsTrigger = false;
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "enter" }));
            fixture.Notify(new CollisionNotification2D(pair, true, false, 1));
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "enter", "exit" }));
        }

        [Test]
        public void StartReceivedBeforeTheFirstUpdateOfACreatedTriggerIsDelivered()
        {
            using var fixture = new SpatialFixture2D();
            var state = new SpatialFixture2D.ColliderState();
            state.Trigger.Value = true;
            var sensor = new Collider2D(fixture.Space, fixture.Bindings(state));
            var enters = 0;
            sensor.TriggerEnter += _ => enters++;
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(sensor, fixture.Second), true, true, 1));
            fixture.Space.Update(0.1f);
            fixture.Space.Update(0.1f);
            Assert.That(enters, Is.EqualTo(1));
        }

        [Test]
        public void InterruptedUpdateKeepsAQueuedStartWhoseKindIsUnchanged()
        {
            using var fixture = new SpatialFixture2D();
            var third = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            var fourth = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            third.IsTrigger = true;
            fixture.Space.Update(0.1f);
            var enters = 0;
            third.TriggerEnter += _ => enters++;
            fixture.First.CollisionEnter += _ => throw new ApplicationException("listener");
            fixture.Send(true);
            fixture.Notify(new CollisionNotification2D(new CollisionPair2D(third, fourth), true, true, 100));
            Assert.Throws<ApplicationException>(() => fixture.Space.Update(0.1f));
            Assert.That(enters, Is.Zero);
            fourth.IsTrigger = true;
            fixture.Space.Update(0.1f);
            Assert.That(enters, Is.EqualTo(1));
        }

        [TestCase("disable"), TestCase("contact"), TestCase("trigger")]
        public void InterruptedUpdateStillEndsTheRelationThatItsSettingsEnded(string change)
        {
            using var fixture = new SpatialFixture2D();
            var trigger = change == "trigger";
            var pair = new CollisionPair2D(fixture.First, fixture.Second);
            var events = new List<string>();
            fixture.First.IsTrigger = trigger;
            fixture.Space.Update(0.1f);
            Listen(fixture.Space, trigger, true, _ => events.Add("enter"));
            Listen(fixture.Space, trigger, false, _ => events.Add("exit"));
            fixture.Notify(new CollisionNotification2D(pair, trigger, true, 1));
            fixture.Space.Update(0.1f);

            Change(fixture, change, true);
            InterruptUpdate(fixture);
            Change(fixture, change, false);
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "enter", "exit" }));

            // The restored settings accept only a new relation.
            fixture.Notify(new CollisionNotification2D(pair, trigger, true, 2));
            fixture.Space.Update(0.1f);
            fixture.Notify(new CollisionNotification2D(pair, trigger, false, 2));
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "enter", "exit", "enter", "exit" }));
        }

        [TestCase("disable"), TestCase("contact"), TestCase("trigger")]
        public void InterruptedUpdateDropsAQueuedStartThatItsSettingsEnded(string change)
        {
            using var fixture = new SpatialFixture2D();
            var trigger = change == "trigger";
            var pair = new CollisionPair2D(fixture.First, fixture.Second);
            var events = new List<string>();
            fixture.First.IsTrigger = trigger;
            fixture.Space.Update(0.1f);
            Listen(fixture.Space, trigger, true, _ => events.Add("enter"));
            Listen(fixture.Space, trigger, false, _ => events.Add("exit"));
            fixture.Notify(new CollisionNotification2D(pair, trigger, true, 1));

            Change(fixture, change, true);
            InterruptUpdate(fixture);
            Change(fixture, change, false);
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.Empty);

            fixture.Notify(new CollisionNotification2D(pair, trigger, true, 2));
            fixture.Space.Update(0.1f);
            fixture.Notify(new CollisionNotification2D(pair, trigger, false, 2));
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "enter", "exit" }));
        }

        [Test]
        public void ExternalInactiveStateEndsRelationshipWithoutChangingEnabled()
        {
            using var fixture = new SpatialFixture2D();
            var exits = 0;
            fixture.Space.CollisionExit += _ => exits++;
            fixture.Send(true);
            fixture.Space.Update(0.1f);
            fixture.FirstState.Active = false;
            fixture.Space.Update(0.1f);
            Assert.That(exits, Is.EqualTo(1));
            Assert.That(fixture.First.Enabled, Is.True);
            Assert.That(fixture.First.IsValid, Is.True);
            fixture.Send(false);
            fixture.Space.Update(0.1f);
            Assert.That(exits, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public void ValidityFailureExcludesObjectBeforeUnhandledOrHandlerFailureEscapes(bool handlerThrows)
        {
            using var fixture = new SpatialFixture2D();
            var sourceFailure = new ApplicationException("validity");
            var handlerFailure = new InvalidOperationException("handler");
            if (handlerThrows) ExceptionHandler.SetHandler(_ => throw handlerFailure);
            fixture.FirstState.Check = () => throw sourceFailure;
            fixture.Second.Enabled = false;
            var observed = Assert.Catch(() => fixture.Space.Update(0.1f));
            Assert.That(observed, Is.SameAs(handlerThrows ? handlerFailure : sourceFailure));
            Assert.That(fixture.First.IsDisposed, Is.True);
            Assert.That(fixture.FirstState.Releases, Is.EqualTo(1));
            Assert.That(fixture.Second.IsValid, Is.True);
            Assert.That(fixture.UpdateCount, Is.Zero);
            Assert.That(fixture.SecondState.Enabled.Value, Is.True);
            Assert.That(fixture.Second.Enabled, Is.False);
            fixture.Space.Update(0.1f);
            Assert.That(fixture.SecondState.Enabled.Value, Is.False);
        }

        [Test]
        public void HandledValidityFailureReturnsFalseAndQueriesKeepHealthyResults()
        {
            using var fixture = new SpatialFixture2D();
            var reports = 0;
            ExceptionHandler.SetHandler(_ => reports++);
            fixture.FirstState.Check = () => throw new ApplicationException("validity");
            fixture.Overlaps.Add(fixture.First);
            fixture.Overlaps.Add(fixture.Second);
            var results = fixture.Space.OverlapCircle(System.Numerics.Vector2.Zero, 2f);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0], Is.SameAs(fixture.Second));
            Assert.That(fixture.First.IsValid, Is.False);
            Assert.That(reports, Is.EqualTo(1));
            Assert.That(fixture.FirstState.Releases, Is.EqualTo(1));
        }

        [TestCase(1e20f), TestCase(1e-23f)]
        public void FiniteNonzeroRayDirectionsAreNormalizedWithoutFloatOverflow(float magnitude)
        {
            using var fixture = new SpatialFixture2D();
            fixture.Space.RaycastAll(System.Numerics.Vector2.Zero, System.Numerics.Vector2.UnitX * magnitude);
            Assert.That(fixture.LastDirection, Is.EqualTo(System.Numerics.Vector2.UnitX));
        }

        [Test]
        public void AdapterIdentifiedFrameworkFailureBypassesSystemHandler()
        {
            using var fixture = new SpatialFixture2D();
            var reports = 0;
            ExceptionHandler.SetHandler(_ => reports++);
            var failure = new SpatialEngineException("Framework operation failed.", new ApplicationException());
            fixture.FirstState.Prepare = () => throw failure;
            Assert.That(Assert.Throws<SpatialEngineException>(() => fixture.Space.Update(0.1f)), Is.SameAs(failure));
            Assert.That(reports, Is.Zero);
            Assert.That(fixture.First.IsValid && fixture.Second.IsValid, Is.True);
            Assert.That(fixture.UpdateCount, Is.Zero);
        }

        [Test]
        public void HandledObjectFailureContinuesRemainingObjectsAndWorldStep()
        {
            using var fixture = new SpatialFixture2D();
            var failures = new List<Exception>();
            ExceptionHandler.SetHandler(failures.Add);
            var failure = new ApplicationException("prepare");
            var prepared = 0;
            var disposed = 0;
            fixture.FirstState.Prepare = () => throw failure;
            fixture.SecondState.Prepare = () => prepared++;
            fixture.First.Disposed += () => disposed++;
            fixture.Space.Update(0.1f);
            Assert.That(failures, Is.EqualTo(new[] { failure }));
            Assert.That(fixture.First.IsDisposed, Is.True);
            Assert.That(fixture.FirstState.Releases, Is.EqualTo(1));
            Assert.That(prepared, Is.EqualTo(1));
            Assert.That(disposed, Is.EqualTo(1));
            Assert.That(fixture.UpdateCount, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public void DisposalCleansEveryChildButUnhandledFailureStopsRemainingNotifications(bool handled)
        {
            using var fixture = new SpatialFixture2D();
            var calls = 0;
            var failures = new List<Exception>();
            if (handled) ExceptionHandler.SetHandler(failures.Add);
            fixture.First.Disposed += () => throw new ApplicationException("disposed");
            fixture.First.Disposed += () => calls++;
            fixture.Space.Disposed += () => calls++;
            if (handled) fixture.Dispose();
            else Assert.Throws<ApplicationException>(fixture.Dispose);
            Assert.That(fixture.First.IsDisposed && fixture.Second.IsDisposed, Is.True);
            Assert.That(fixture.FirstState.Releases, Is.EqualTo(1));
            Assert.That(fixture.SecondState.Releases, Is.EqualTo(1));
            Assert.That(fixture.Releases, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(handled ? 2 : 0));
            Assert.That(failures.Count, Is.EqualTo(handled ? 1 : 0));
        }

        [Test]
        public void NestedValidityFailureIsNotReportedAgainOrAttributedToTheHealthyCaller()
        {
            using var fixture = new SpatialFixture2D();
            var failure = new ApplicationException("nested validity");
            var reports = 0;
            var calls = 0;
            ExceptionHandler.SetHandler(exception => { reports++; throw exception; });
            fixture.Space.CollisionEnter += _ =>
            {
                calls++;
                fixture.SecondState.Check = () => throw failure;
                var valid = fixture.Second.IsValid;
                Assert.That(valid, Is.False);
            };
            fixture.Send(true);
            Assert.That(Assert.Throws<ApplicationException>(() => fixture.Space.Update(0.1f)), Is.SameAs(failure));
            Assert.That(reports, Is.EqualTo(1));
            Assert.That(fixture.Second.IsDisposed, Is.True);
            Assert.That(fixture.Space.IsValid && fixture.First.IsValid, Is.True);

            var third = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(fixture.First, third), false, true, 100));
            fixture.Space.Update(0.1f);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(reports, Is.EqualTo(1));
        }

        [Test]
        public void ColliderListenerFailureExcludesItsPairAndKeepsTheWorld(
            [Values] bool trigger, [Values] bool entering)
        {
            using var fixture = new SpatialFixture2D();
            var failures = new List<Exception>();
            ExceptionHandler.SetHandler(failures.Add);
            var failure = new ApplicationException("listener");
            var third = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            var fourth = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            var spaceCalls = 0;
            var secondCalls = 0;
            var thirdCalls = 0;
            var disposed = new List<string>();
            fixture.First.Disposed += () => disposed.Add("first");
            fixture.Second.Disposed += () => disposed.Add("second");
            fixture.First.IsTrigger = trigger;
            third.IsTrigger = trigger;
            fixture.Space.Update(0.1f);
            Listen(fixture.Space, trigger, entering, _ => spaceCalls++);
            Listen(fixture.First, trigger, entering, _ => throw failure);
            Listen(fixture.Second, trigger, entering, _ => secondCalls++);
            Listen(third, trigger, entering, _ => thirdCalls++);

            var healthy = new CollisionPair2D(third, fourth);
            fixture.Send(true, trigger);
            if (!entering) fixture.Send(false, trigger);
            fixture.Notify(new CollisionNotification2D(healthy, trigger, true, 100));
            if (!entering) fixture.Notify(new CollisionNotification2D(healthy, trigger, false, 100));
            fixture.Space.Update(0.1f);

            Assert.That(failures, Is.EqualTo(new[] { failure }));
            Assert.That(fixture.First.IsDisposed && fixture.Second.IsDisposed, Is.True);
            Assert.That(fixture.FirstState.Releases, Is.EqualTo(1));
            Assert.That(fixture.SecondState.Releases, Is.EqualTo(1));
            Assert.That(disposed, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(secondCalls, Is.Zero);
            Assert.That(spaceCalls, Is.EqualTo(2));
            Assert.That(thirdCalls, Is.EqualTo(1));
            Assert.That(fixture.Space.IsValid && third.IsValid && fourth.IsValid, Is.True);
            Assert.That(fixture.Releases, Is.Zero);
        }

        [Test]
        public void SpaceListenerFailureEndsOnlyItsOwnSubscription(
            [Values] bool trigger, [Values] bool entering)
        {
            using var fixture = new SpatialFixture2D();
            var failures = new List<Exception>();
            ExceptionHandler.SetHandler(failures.Add);
            var failure = new ApplicationException("listener");
            var failingCalls = 0;
            var calls = new List<string>();
            fixture.First.IsTrigger = trigger;
            fixture.Space.Update(0.1f);
            Listen(fixture.Space, trigger, entering, _ =>
            {
                failingCalls++;
                throw failure;
            });
            Listen(fixture.Space, trigger, entering, _ => calls.Add("space"));
            Listen(fixture.First, trigger, entering, _ => calls.Add("first"));
            Listen(fixture.Second, trigger, entering, _ => calls.Add("second"));

            for (var step = 0; step < 2; step++)
            {
                fixture.Send(true, trigger);
                fixture.Send(false, trigger);
                fixture.Space.Update(0.1f);
            }

            Assert.That(failures, Is.EqualTo(new[] { failure }));
            Assert.That(failingCalls, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(new[] { "space", "first", "second", "space", "first", "second" }));
            Assert.That(fixture.Space.IsValid && fixture.First.IsValid && fixture.Second.IsValid, Is.True);
            Assert.That(fixture.FirstState.Releases + fixture.SecondState.Releases + fixture.Releases, Is.Zero);
        }

        [Test]
        public void UnhandledListenerFailureIsSettledBeforeItStopsTheUpdate(
            [Values] bool fromSpace, [Values] bool handlerThrows)
        {
            using var fixture = new SpatialFixture2D();
            var failure = new ApplicationException("listener");
            var handlerFailure = new InvalidOperationException("handler");
            if (handlerThrows) ExceptionHandler.SetHandler(_ => throw handlerFailure);
            var third = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            var fourth = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            var failingCalls = 0;
            var secondCalls = 0;
            var thirdCalls = 0;
            void Fail(CollisionPair2D pair)
            {
                failingCalls++;
                throw failure;
            }

            if (fromSpace) fixture.Space.CollisionEnter += Fail;
            else fixture.First.CollisionEnter += Fail;
            fixture.Second.CollisionEnter += _ => secondCalls++;
            third.CollisionEnter += _ => thirdCalls++;
            fixture.Send(true);
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(third, fourth), false, true, 100));

            var observed = Assert.Catch(() => fixture.Space.Update(0.1f));
            Assert.That(observed, Is.SameAs(handlerThrows ? handlerFailure : failure));
            Assert.That(fixture.First.IsDisposed, Is.EqualTo(!fromSpace));
            Assert.That(fixture.Second.IsDisposed, Is.EqualTo(!fromSpace));
            Assert.That(fixture.FirstState.Releases, Is.EqualTo(fromSpace ? 0 : 1));
            Assert.That(fixture.SecondState.Releases, Is.EqualTo(fromSpace ? 0 : 1));
            Assert.That(secondCalls + thirdCalls, Is.Zero);
            Assert.That(fixture.Space.IsValid && third.IsValid && fourth.IsValid, Is.True);

            // The pair that was still queued is delivered by the next update without the ended subscription.
            fixture.Space.Update(0.1f);
            Assert.That(failingCalls, Is.EqualTo(1));
            Assert.That(thirdCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.Zero);
        }

        [Test]
        public void ReadFailureInsideAListenerExcludesOnlyTheFailedObject(
            [Values] bool fromSpace, [Values] bool inPair, [Values] bool caught)
        {
            using var fixture = new SpatialFixture2D();
            var failures = new List<Exception>();
            ExceptionHandler.SetHandler(failures.Add);
            var failure = new ApplicationException("pose");
            var state = new SpatialFixture2D.ColliderState();
            var failed = new Collider2D(fixture.Space, new ColliderBindings2D(
                new SpatialProperty<Transform2D>(() => throw failure, _ => { }),
                state.Enabled.Property, state.Trigger.Property, state.Layer.Property,
                () => state.Alive, () => state.Releases++));
            var third = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            var calls = 0;
            var laterCalls = 0;
            void Read(CollisionPair2D pair)
            {
                calls++;
                if (!failed.IsValid) return;
                try { _ = failed.Pose; }
                catch (ObjectDisposedException) when (caught) { }
            }

            if (fromSpace) fixture.Space.CollisionEnter += Read;
            else fixture.Second.CollisionEnter += Read;
            fixture.Second.CollisionEnter += _ => laterCalls++;
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(inPair ? failed : fixture.First, fixture.Second), false, true, 1));
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(third, fixture.Second), false, true, 2));
            fixture.Space.Update(0.1f);

            Assert.That(failures, Is.EqualTo(new[] { failure }));
            Assert.That(failed.IsDisposed, Is.True);
            Assert.That(state.Releases, Is.EqualTo(1));
            Assert.That(fixture.Space.IsValid && fixture.First.IsValid, Is.True);
            Assert.That(fixture.Second.IsValid && third.IsValid, Is.True);
            Assert.That(calls, Is.EqualTo(2));

            // The interrupted notification goes on only while its own pair is still valid.
            Assert.That(laterCalls, Is.EqualTo(inPair ? 1 : 2));
        }

        [Test]
        public void UsingADisposedHandleIsAFailureOfTheListener([Values] bool fromSpace)
        {
            using var fixture = new SpatialFixture2D();
            var failures = new List<Exception>();
            ExceptionHandler.SetHandler(failures.Add);
            var third = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            third.Dispose();
            var calls = 0;
            void Read(CollisionPair2D pair)
            {
                calls++;
                _ = third.Pose;
            }

            if (fromSpace) fixture.Space.CollisionEnter += Read;
            else fixture.First.CollisionEnter += Read;
            for (var step = 0; step < 2; step++)
            {
                fixture.Send(true);
                fixture.Send(false);
                fixture.Space.Update(0.1f);
            }

            Assert.That(failures.Count, Is.EqualTo(1));
            Assert.That(failures[0], Is.TypeOf<ObjectDisposedException>());
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(fixture.First.IsDisposed, Is.EqualTo(!fromSpace));
            Assert.That(fixture.Second.IsDisposed, Is.EqualTo(!fromSpace));
            Assert.That(fixture.Space.IsValid, Is.True);
        }

        [Test]
        public void FrameworkFailureInsideAListenerChangesNothing()
        {
            using var fixture = new SpatialFixture2D();
            var reports = 0;
            ExceptionHandler.SetHandler(_ => reports++);
            var failure = new SpatialEngineException("Framework operation failed.", new ApplicationException());
            var calls = 0;
            fixture.Space.CollisionEnter += _ =>
            {
                if (++calls == 1) throw failure;
            };
            fixture.Send(true);
            fixture.Send(false);
            Assert.That(Assert.Throws<SpatialEngineException>(() => fixture.Space.Update(0.1f)), Is.SameAs(failure));
            fixture.Send(true);
            fixture.Space.Update(0.1f);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(reports, Is.Zero);
            Assert.That(fixture.Space.IsValid && fixture.First.IsValid && fixture.Second.IsValid, Is.True);
        }

        [TestCase("handled"), TestCase("unhandled"), TestCase("handler throws")]
        public void InvalidAdapterNotificationIsDroppedAloneAndKeepsTheWorld(string policy)
        {
            using var fixture = new SpatialFixture2D();
            using var foreign = new SpatialFixture2D();
            var failures = new List<Exception>();
            var handlerFailure = new ApplicationException("handler");
            if (policy == "handled") ExceptionHandler.SetHandler(failures.Add);
            else if (policy == "handler throws") ExceptionHandler.SetHandler(_ => throw handlerFailure);
            var enters = 0;
            fixture.Space.CollisionEnter += _ => enters++;
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(fixture.First, foreign.First), false, true, 1));
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(fixture.First, fixture.Second), false, true, 2));

            if (policy == "handled")
            {
                fixture.Space.Update(0.1f);
                Assert.That(failures.Count, Is.EqualTo(1));
                Assert.That(failures[0], Is.TypeOf<InvalidOperationException>());
            }
            else
            {
                var observed = Assert.Catch(() => fixture.Space.Update(0.1f));
                if (policy == "unhandled") Assert.That(observed, Is.TypeOf<InvalidOperationException>());
                else Assert.That(observed, Is.SameAs(handlerFailure));
                Assert.That(enters, Is.Zero);
                fixture.Space.Update(0.1f);
            }

            Assert.That(enters, Is.EqualTo(1));
            Assert.That(fixture.Space.IsValid && fixture.First.IsValid && fixture.Second.IsValid, Is.True);
            Assert.That(foreign.Space.IsValid && foreign.First.IsValid, Is.True);
            Assert.That(fixture.Releases + fixture.FirstState.Releases + fixture.SecondState.Releases, Is.Zero);
        }

        [TestCase("handled"), TestCase("unhandled"), TestCase("handler throws")]
        public void AdapterStepFailureEndsOnlyThatUpdateAndKeepsTheWorld(string policy)
        {
            using var fixture = new SpatialFixture2D();
            var third = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            var fourth = new Collider2D(fixture.Space, fixture.Bindings(new SpatialFixture2D.ColliderState()));
            var failures = new List<Exception>();
            var stepFailure = new ApplicationException("step");
            var handlerFailure = new InvalidOperationException("handler");
            var events = new List<string>();
            fixture.Space.CollisionEnter += _ => events.Add("enter");
            fixture.Space.CollisionExit += _ => events.Add("exit");
            fixture.Send(true);
            fixture.Space.Update(0.1f);
            events.Clear();
            if (policy == "handled") ExceptionHandler.SetHandler(failures.Add);
            else if (policy == "handler throws") ExceptionHandler.SetHandler(_ => throw handlerFailure);

            // The failed step leaves an applied disable and a reported start undelivered.
            fixture.First.Enabled = false;
            fixture.OnUpdate = _ =>
            {
                fixture.Notify(new CollisionNotification2D(
                    new CollisionPair2D(third, fourth), false, true, 100));
                throw stepFailure;
            };
            if (policy == "handled")
            {
                fixture.Space.Update(0.1f);
                Assert.That(failures, Is.EqualTo(new[] { stepFailure }));
            }
            else
            {
                var observed = Assert.Catch(() => fixture.Space.Update(0.1f));
                Assert.That(observed, Is.SameAs(policy == "unhandled" ? stepFailure : (Exception)handlerFailure));
            }

            Assert.That(events, Is.Empty);
            Assert.That(fixture.Space.IsValid && fixture.First.IsValid && fixture.Second.IsValid, Is.True);
            Assert.That(third.IsValid && fourth.IsValid, Is.True);
            Assert.That(fixture.Releases, Is.Zero);
            fixture.OnUpdate = null;
            fixture.Space.Update(0.1f);
            Assert.That(events, Is.EqualTo(new[] { "exit", "enter" }));
        }

        private static void Change(SpatialFixture2D fixture, string change, bool ended)
        {
            if (change == "disable") fixture.First.Enabled = !ended;
            else fixture.First.IsTrigger = ended != (change == "trigger");
        }

        // The settings are applied before the engine step fails, so nothing is delivered.
        private static void InterruptUpdate(SpatialFixture2D fixture)
        {
            fixture.OnUpdate = _ => throw new SpatialEngineException("Native step failed.", new ApplicationException());
            Assert.Throws<SpatialEngineException>(() => fixture.Space.Update(0.1f));
            fixture.OnUpdate = null;
        }

        private static void Listen(
            CollisionSpace2D space, bool trigger, bool entering, Action<CollisionPair2D> listener)
        {
            if (trigger && entering) space.TriggerEnter += listener;
            else if (trigger) space.TriggerExit += listener;
            else if (entering) space.CollisionEnter += listener;
            else space.CollisionExit += listener;
        }

        private static void Listen(
            Collider2D collider, bool trigger, bool entering, Action<CollisionPair2D> listener)
        {
            if (trigger && entering) collider.TriggerEnter += listener;
            else if (trigger) collider.TriggerExit += listener;
            else if (entering) collider.CollisionEnter += listener;
            else collider.CollisionExit += listener;
        }
    }
}
