using System;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Samples
{
    public static class LifecycleHubSample
    {
        // Content
        public static void Run()
        {
            var door = new Door();
            var sensor = new DoorSensor();
            var minimap = new object();
            var sound = new object();

            sensor.Watch(door);
            door.LifecycleHub.Connect(minimap).OnDisposed(() => Console.WriteLine("Minimap: door icon removed"));
            door.LifecycleHub.Connect(sound).OnDisposed(() => Console.WriteLine("Sound: not called"));

            // A listener that no longer needs the door disconnects before it is disposed.
            door.LifecycleHub.Disconnect(sound);

            // The order between listeners is not guaranteed.
            door.Dispose();
            Console.WriteLine($"Door disposed: {door.IsDisposed}, sensor has target: {sensor.HasTarget}");
        }

        private class Door : ILifecycleSubject
        {
            // Front
            public bool IsDisposed => _hub.IsDisposed;
            public ILifecycleHub LifecycleHub => _hub;

            // Internal
            private readonly LifecycleHub _hub = new();


            // Content
            public void Dispose() => _hub.Dispose();
        }

        private class DoorSensor
        {
            // Front
            public bool HasTarget => _target != null;

            // Internal
            private Door _target;


            // Content
            public void Watch(Door door)
            {
                _target = door;
                door.LifecycleHub.Connect(this).OnDisposed(() =>
                {
                    Console.WriteLine("Sensor: door removed, reference cleared");
                    _target = null;
                });
            }
        }
    }
}
