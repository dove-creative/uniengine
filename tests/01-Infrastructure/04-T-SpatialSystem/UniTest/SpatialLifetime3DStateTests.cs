using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [TestFixture, Category("SpatialSystem")]
    public class SpatialLifetime3DStateTests
    {
        private static readonly string[] _actions =
        {
            "ContactEnter", "ContactExit", "TriggerEnter", "TriggerExit", "Write", "Probe",
            "Update", "Configure", "Create", "ToggleEnabled", "ToggleTrigger",
            "DisposeCollider", "DestroyCollider", "DisposeSpace", "DestroyWorld", "Reset",
        };


        [Test]
        public async Task FixedLifetimePaths()
        {
            var path = new[]
            {
                "ContactExit", "ContactEnter", "ContactEnter", "ToggleEnabled", "ToggleEnabled", "Update",
                "ToggleTrigger", "Update", "TriggerEnter", "TriggerEnter", "ToggleEnabled", "Update",
                "ToggleEnabled", "Update", "TriggerEnter", "ToggleTrigger", "Update",
                "ContactExit", "ContactEnter", "TriggerExit", "TriggerEnter", "Write", "Probe",
                "DisposeCollider", "Write", "ContactEnter", "TriggerExit", "DisposeCollider", "Probe",
                "Reset", "DestroyCollider", "Probe", "Write", "DisposeSpace", "ContactEnter", "Probe",
                "Reset", "DestroyWorld", "Write", "TriggerEnter", "DisposeSpace", "Probe",
                "Reset", "Update", "Configure", "Create", "ToggleEnabled", "ToggleTrigger", "Update",
                "DisposeSpace", "Update", "Create", "Configure", "Probe",
            };
            var project = new InfrastructureContinuousProject(
                history => history.Length < path.Length ? new[] { path[history.Length] } : Array.Empty<string>(),
                VerifyHistory);
            var result = await project.Execute(path.Length);
            Assert.That(result.AllSucceed(out var cancelled), Is.True);
            Assert.That(cancelled, Is.False);
        }

        [TestCase(0), TestCase(1), TestCase(2), Category("RandomContinuous")]
        public Task RandomLifetimePath(int seed) => RandomContinuousTest.RunAsync(
            new InfrastructureContinuousProject(history => _actions, VerifyHistory), seed);

        private static void VerifyHistory(string[] history)
        {
            SpatialFixture3D fixture = null;
            var worldAlive = true;
            var colliderAlive = true;
            var contact = false;
            var trigger = false;
            var requestedEnabled = true;
            var requestedTrigger = false;
            var appliedEnabled = true;
            var appliedTrigger = false;
            var expected = 0;
            var spaceEvents = 0;
            var firstEvents = 0;
            var secondEvents = 0;
            var updates = 0;
            var disposed = 0;

            void Reset()
            {
                fixture?.Dispose();
                fixture = new SpatialFixture3D();
                worldAlive = colliderAlive = true;
                contact = trigger = false;
                requestedEnabled = appliedEnabled = true;
                requestedTrigger = appliedTrigger = false;
                expected = spaceEvents = firstEvents = secondEvents = updates = disposed = 0;
                fixture.Space.Disposed += () => disposed++;
                fixture.Space.CollisionEnter += pair => spaceEvents++;
                fixture.Space.CollisionExit += pair => spaceEvents++;
                fixture.Space.TriggerEnter += pair => spaceEvents++;
                fixture.Space.TriggerExit += pair => spaceEvents++;
                fixture.First.CollisionEnter += pair => firstEvents++;
                fixture.First.CollisionExit += pair => firstEvents++;
                fixture.First.TriggerEnter += pair => firstEvents++;
                fixture.First.TriggerExit += pair => firstEvents++;
                fixture.Second.CollisionEnter += pair => secondEvents++;
                fixture.Second.CollisionExit += pair => secondEvents++;
                fixture.Second.TriggerEnter += pair => secondEvents++;
                fixture.Second.TriggerExit += pair => secondEvents++;
            }

            void Step(Action native = null, bool? nativeKind = null, bool entering = false)
            {
                if (!worldAlive)
                {
                    Assert.Throws<ObjectDisposedException>(() => fixture.Space.Update(0.125f));
                    return;
                }
                if (colliderAlive)
                {
                    appliedEnabled = requestedEnabled;
                    appliedTrigger = requestedTrigger;
                    if (contact && (!appliedEnabled || appliedTrigger)) { contact = false; expected++; }
                    if (trigger && (!appliedEnabled || !appliedTrigger)) { trigger = false; expected++; }
                    if (nativeKind.HasValue && appliedEnabled && nativeKind.Value == appliedTrigger)
                    {
                        var current = appliedTrigger ? trigger : contact;
                        if (current != entering) expected++;
                        if (appliedTrigger) trigger = entering;
                        else contact = entering;
                    }
                }
                fixture.OnUpdate = _ => native?.Invoke();
                fixture.Space.Update(0.125f);
                fixture.OnUpdate = null;
                updates++;
            }

            Reset();
            try
            {
                foreach (var action in history)
                {
                    switch (action)
                    {
                        case "ContactEnter":
                        case "ContactExit":
                        case "TriggerEnter":
                        case "TriggerExit":
                            var isTrigger = action.StartsWith("Trigger", StringComparison.Ordinal);
                            var entering = action.EndsWith("Enter", StringComparison.Ordinal);
                            Step(
                                () => fixture.Send(entering, isTrigger, reverse: expected % 2 == 0),
                                isTrigger,
                                entering);
                            break;
                        case "Update": Step(); break;
                        case "ToggleEnabled":
                            if (worldAlive && colliderAlive)
                                fixture.First.Enabled = requestedEnabled = !requestedEnabled;
                            else Assert.Throws<ObjectDisposedException>(() => fixture.First.Enabled = false);
                            break;
                        case "ToggleTrigger":
                            if (worldAlive && colliderAlive)
                                fixture.First.IsTrigger = requestedTrigger = !requestedTrigger;
                            else Assert.Throws<ObjectDisposedException>(() => fixture.First.IsTrigger = true);
                            break;
                        case "Configure":
                            if (worldAlive)
                            {
                                fixture.Space.SetLayerCollision(2, 7, false);
                                Assert.That(fixture.Space.GetLayerCollision(7, 2), Is.False);
                            }
                            else
                                Assert.Throws<ObjectDisposedException>(() => fixture.Space.SetLayerCollision(
                                    2,
                                    7,
                                    false));
                            break;
                        case "Create":
                            if (worldAlive)
                            {
                                var created = fixture.Space.CreateBoxCollider(System.Numerics.Vector3.One);
                                Assert.That(created.Space, Is.SameAs(fixture.Space));
                                created.Dispose();
                            }
                            else
                                Assert.Throws<ObjectDisposedException>(
                                    () => fixture.Space.CreateBoxCollider(System.Numerics.Vector3.One));
                            break;
                        case "Write":
                            if (worldAlive && colliderAlive)
                            {
                                fixture.First.Layer = 7;
                                Assert.That(fixture.FirstState.Layer.Value, Is.EqualTo(7));
                            }
                            else Assert.Throws<ObjectDisposedException>(() => fixture.First.Layer = 7);
                            break;
                        case "DisposeCollider":
                            fixture.First.Dispose();
                            colliderAlive = false;
                            contact = trigger = false;
                            break;
                        case "DestroyCollider":
                            fixture.FirstState.Alive = false;
                            colliderAlive = false;
                            contact = trigger = false;
                            break;
                        case "DisposeSpace":
                            fixture.Space.Dispose();
                            worldAlive = colliderAlive = false;
                            contact = trigger = false;
                            break;
                        case "DestroyWorld":
                            fixture.Alive = false;
                            worldAlive = colliderAlive = false;
                            contact = trigger = false;
                            break;
                        case "Reset": Reset(); break;
                        case "Probe": break;
                        default: throw new ArgumentException(action);
                    }

                    Assert.That(fixture.Space.IsValid, Is.EqualTo(worldAlive), action);
                    Assert.That(fixture.First.IsValid, Is.EqualTo(worldAlive && colliderAlive), action);
                    Assert.That(fixture.Second.IsValid, Is.EqualTo(worldAlive), action);
                    Assert.That(spaceEvents, Is.EqualTo(expected), action);
                    Assert.That(firstEvents, Is.EqualTo(expected), action);
                    Assert.That(secondEvents, Is.EqualTo(expected), action);
                    Assert.That(fixture.FirstState.Releases, Is.EqualTo(colliderAlive ? 0 : 1), action);
                    Assert.That(fixture.Releases, Is.EqualTo(worldAlive ? 0 : 1), action);
                    Assert.That(fixture.UpdateCount, Is.EqualTo(updates), action);
                    Assert.That(disposed, Is.EqualTo(worldAlive ? 0 : 1), action);
                    Assert.That(fixture.Disconnects, Is.EqualTo(worldAlive ? 0 : 1), action);
                }
            }
            finally { fixture.Dispose(); }
        }
    }
}
