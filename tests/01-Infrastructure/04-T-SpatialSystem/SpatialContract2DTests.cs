using System;
using System.Collections.Generic;
using System.Numerics;
using BlackThunder.UniEngine.Objects;
using BlackThunder.UniEngine.SpatialSystem;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture, Category("SpatialSystem")]
    public class SpatialContract2DTests
    {
        [Test]
        public void PropertiesAccessExternalState()
        {
            using var fixture = new SpatialFixture2D();
            var pose = new Transform2D(new Vector2(2f, 4f), 90f);
            fixture.First.Pose = pose;
            fixture.First.Enabled = false;
            fixture.First.IsTrigger = true;
            fixture.First.Layer = 9;
            Assert.That(fixture.FirstState.Enabled.Value, Is.True);
            Assert.That(fixture.FirstState.Trigger.Value, Is.False);
            fixture.Space.Update(0.01f);
            Assert.That(fixture.FirstState.Pose.Value, Is.EqualTo(pose));
            Assert.That(fixture.FirstState.Enabled.Value, Is.False);
            Assert.That(fixture.FirstState.Trigger.Value, Is.True);
            Assert.That(fixture.FirstState.Layer.Value, Is.EqualTo(9));
            fixture.FirstState.Enabled.Value = true;
            Assert.That(fixture.First.Enabled, Is.True);
        }

        [Test]
        public void QueriesPreserveHandlesSortHitsAndSnapshotAllResults()
        {
            using var fixture = new SpatialFixture2D();
            for (var index = 40; index >= 1; index--)
                fixture.Hits.Add(new SpatialRaycastHit2D(fixture.First, Vector2.UnitX * index, -Vector2.UnitX, index));
            var filter = new SpatialQueryFilter(1 << 9, false);
            var hits = fixture.Space.RaycastAll(Vector2.Zero, Vector2.UnitX * 8f, 100f, filter);
            Assert.That(hits.Count, Is.EqualTo(40));
            Assert.That(hits[0].Distance, Is.EqualTo(1f));
            Assert.That(hits[39].Distance, Is.EqualTo(40f));
            Assert.That(hits[0].Collider, Is.SameAs(fixture.First));
            Assert.That(hits[0].Point, Is.EqualTo(Vector2.UnitX));
            Assert.That(hits[0].Normal, Is.EqualTo(-Vector2.UnitX));
            Assert.That(fixture.LastDirection, Is.EqualTo(Vector2.UnitX));
            Assert.That(fixture.LastFilter.LayerMask, Is.EqualTo(1 << 9));
            Assert.That(fixture.LastFilter.IncludeTriggers, Is.False);
            Assert.That(fixture.Space.Raycast(Vector2.Zero, Vector2.UnitX, out var nearest), Is.True);
            Assert.That(nearest.Distance, Is.EqualTo(1f));
            fixture.Hits.Clear();
            Assert.That(hits.Count, Is.EqualTo(40));
            Assert.That(fixture.Space.Raycast(Vector2.Zero, Vector2.UnitX, out _), Is.False);
            fixture.Overlaps.AddRange(new[] { fixture.First, fixture.Second, fixture.First });
            var overlaps = fixture.Space.OverlapCircle(Vector2.Zero, 2f, filter);
            Assert.That(overlaps, Is.EqualTo(new[] { fixture.First, fixture.Second }));
            fixture.Space.OverlapBox(new Transform2D(new Vector2(2f, 4f), 90f), Vector2.One, filter);
            fixture.Space.OverlapCapsule(
                new Transform2D(Vector2.Zero, 30f),
                Vector2.One,
                CapsuleDirection2D.Horizontal,
                filter);
            Assert.That(fixture.LastFilter.IncludeTriggers, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PairTransitionsDeduplicateBothSidesAndKeepExitReentry(bool trigger)
        {
            using var fixture = new SpatialFixture2D();
            var events = new List<string>();
            void SpaceEnter(CollisionPair2D pair)
            {
                Assert.That(pair.Other(fixture.First), Is.SameAs(fixture.Second));
                events.Add("space enter");
            }
            fixture.First.IsTrigger = trigger;
            fixture.Space.Update(0.01f);
            if (trigger)
            {
                fixture.Space.TriggerEnter += SpaceEnter;
                fixture.Space.TriggerExit += pair => events.Add("space exit");
                fixture.First.TriggerEnter += pair => events.Add("first");
                fixture.Second.TriggerEnter += pair => events.Add("second");
            }
            else
            {
                fixture.Space.CollisionEnter += SpaceEnter;
                fixture.Space.CollisionExit += pair => events.Add("space exit");
                fixture.First.CollisionEnter += pair => events.Add("first");
                fixture.Second.CollisionEnter += pair => events.Add("second");
            }
            fixture.Send(false, trigger);
            if (fixture.Space.IsValid) fixture.Space.Update(0.01f);
            fixture.Send(true, trigger);
            if (fixture.Space.IsValid) fixture.Space.Update(0.01f);
            fixture.Send(true, trigger, true);
            if (fixture.Space.IsValid) fixture.Space.Update(0.01f);
            fixture.Send(false, trigger, true);
            if (fixture.Space.IsValid) fixture.Space.Update(0.01f);
            fixture.Send(false, trigger);
            if (fixture.Space.IsValid) fixture.Space.Update(0.01f);
            fixture.Send(true, trigger);
            if (fixture.Space.IsValid) fixture.Space.Update(0.01f);
            Assert.That(events, Is.EqualTo(new[]
            {
                "space enter", "first", "second", "space exit", "space enter", "first", "second",
            }));
            Assert.That(fixture.QueryCount, Is.Zero);
        }

        [Test]
        public void DisposalDuringNotificationStopsLaterCallbacks()
        {
            using var fixture = new SpatialFixture2D();
            var calls = 0;
            fixture.Space.CollisionEnter += pair => fixture.First.Dispose();
            fixture.Space.CollisionEnter += pair => calls++;
            fixture.First.CollisionEnter += pair => calls++;
            fixture.Second.CollisionEnter += pair => calls++;
            fixture.Send(true);
            if (fixture.Space.IsValid) fixture.Space.Update(0.01f);
            Assert.That(calls, Is.Zero);
            fixture.Send(false);
            if (fixture.Space.IsValid) fixture.Space.Update(0.01f);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void InvalidationAndDisposalReleaseOnceAndIgnoreLateNotifications()
        {
            using var fixture = new SpatialFixture2D();
            var calls = 0;
            fixture.Space.CollisionEnter += pair => calls++;
            fixture.FirstState.Alive = false;
            Assert.That(fixture.First.IsValid, Is.False);
            Assert.That(fixture.First.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => _ = fixture.First.Pose);
            fixture.First.Dispose();
            Assert.That(fixture.FirstState.Releases, Is.EqualTo(1));
            fixture.Send(true);
            if (fixture.Space.IsValid) fixture.Space.Update(0.01f);
            Assert.That(calls, Is.Zero);
            fixture.Alive = false;
            Assert.That(fixture.Space.IsValid, Is.False);
            Assert.That(fixture.Second.IsDisposed, Is.True);
            fixture.Send(true);
            if (fixture.Space.IsValid) fixture.Space.Update(0.01f);
            Assert.That(calls, Is.Zero);
            Assert.That(fixture.Disconnects, Is.EqualTo(1));
            Assert.That(fixture.SecondState.Alive, Is.True);
        }

        [Test]
        public void InvalidValuesAndForeignResultsFailBeforeMutation()
        {
            using var fixture = new SpatialFixture2D();
            using var other = new SpatialFixture2D();
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.First.Layer = 32);
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Space.RaycastAll(Vector2.Zero, Vector2.Zero));
            fixture.Overlaps.Add(other.First);
            Assert.Throws<InvalidOperationException>(() => fixture.Space.OverlapCircle(Vector2.Zero, 1f));
            fixture.Notify(new CollisionNotification2D(
                new CollisionPair2D(fixture.First, other.First), false, true, 1));
            Assert.Throws<InvalidOperationException>(() => fixture.Space.Update(0.01f));
            Assert.That(other.First.IsValid, Is.True);
        }
    }
}
