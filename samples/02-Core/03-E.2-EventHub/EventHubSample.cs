using System;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Samples
{
    public static class EventHubSample
    {
        // Content
        public static void Run()
        {
            var unit = new Unit(10);
            var healthBar = new HealthBar();
            var combatLog = new object();

            healthBar.Watch(unit);
            unit.EventHub.Connect(combatLog).OnUpdated(health => Console.WriteLine($"Combat log: health {health}"));

            unit.Damage(4);

            // Disconnect removes every callback of that listener at once.
            unit.EventHub.Disconnect(combatLog);

            // Updates are skipped while the hub sleeps; the wake-up callback publishes the final value once.
            using (unit.BeginBatch())
            {
                unit.Damage(1);
                unit.Damage(1);
                Console.WriteLine("Batch: two hits applied while asleep");
            }

            unit.Damage(7);
            unit.Dispose();
            Console.WriteLine($"Health bar has target: {healthBar.HasTarget}");
        }

        private class Unit : IEventSubject<int>
        {
            // Front
            public bool IsDisposed => _hub.IsDisposed;
            public IEventHub<int> EventHub => _hub;

            // Internal
            private readonly EventHub<int> _hub;
            private int _health;


            // Content
            public Unit(int health)
            {
                _health = health;
                _hub = new EventHub<int>(this);
            }

            public void Damage(int amount)
            {
                _health = Math.Max(0, _health - amount);
                _hub.Update(_health);
            }

            public IDisposable BeginBatch() => _hub.Sleep(() => _hub.Update(_health));

            public void Dispose() => _hub.Dispose();
        }

        private class HealthBar
        {
            // Front
            public bool HasTarget => _target != null;

            // Internal
            private Unit _target;


            // Content
            public void Watch(Unit unit)
            {
                _target = unit;
                unit.EventHub.Connect(this)
                    .OnUpdated(health => Console.WriteLine($"Health bar: {health}"))
                    .OnUpdatedWhen(0, () => Console.WriteLine("Health bar: defeated"))
                    .OnDisposed(() => _target = null);
            }
        }
    }
}
