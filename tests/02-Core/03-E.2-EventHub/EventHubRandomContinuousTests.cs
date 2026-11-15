using System;
using System.Collections.Generic;
using System.Diagnostics;
using BlackThunder.UniEngine.Core;
using BlackThunder.UniEngine.Infrastructure;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    [NonParallelizable]
    public class EventHubRandomContinuousTests
    {
        // Internal
        [Flags]
        private enum Changes { Shape = 1, Location = 2 }

        [Flags]
        private enum AlternateChanges : byte { Shape = 1, Location = 2 }

        private enum Route { Root, Target, Predicate, Flags, CombinedPredicate, Object, Parameterless }

        private class Listener
        {
            public readonly object Key = new();
            public readonly bool[] Registered = new bool[7];
            public readonly Action<ulong>[] Callbacks = new Action<ulong>[7];
            public IEventHubBuilder<ulong> Builder;
            public IEventHubBuilder<ulong> StaleBuilder;
            public Action<object> ObjectCallback;
            public Action ParameterlessCallback;
        }


        // Content
        [SetUp]
        public void SetUp() => ExceptionHandler.SetHandler(null);

        [TearDown]
        public void TearDown() => ExceptionHandler.SetHandler(null);

        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void CombinedRegistrationsContinueWithRandomSeed(int seed)
        {
            const int depth = RandomContinuousTest.DefaultDepth;
            var random = new Random(seed);
            var received = new List<string>();
            var listeners = new Listener[4];
            var hub = new EventHub<ulong>(new object());
            var disposing = 0;
            var disposed = 0;
            var reachedDepth = 0;
            var stopwatch = Stopwatch.StartNew();
            Func<ulong, bool> predicate = value => value > 2;
            Func<object, bool> objectPredicate = value => (ulong)value < 4;
            Func<ulong, bool> combinedPredicate = value => (value & 1) != 0 && value > 2;

            for (int index = 0; index < listeners.Length; index++)
            {
                var listenerIndex = index;
                var listener = new Listener();
                listeners[index] = listener;

                for (int route = 0; route < listener.Callbacks.Length; route++)
                {
                    var routeIndex = route;
                    listener.Callbacks[route] = value => received.Add($"{listenerIndex}:{routeIndex}:{value}");
                }

                listener.ObjectCallback = value => received.Add($"{listenerIndex}:{(int)Route.Object}:{value}");
                listener.ParameterlessCallback = () => received.Add($"{listenerIndex}:{(int)Route.Parameterless}");
            }

            try
            {
                for (int step = 0; step < depth; step++)
                {
                    var index = random.Next(listeners.Length);
                    var listener = listeners[index];
                    var operation = random.Next(8);
                    var route = (Route)random.Next(listener.Registered.Length);
                    var value = (ulong)random.Next(8);
                    TestContext.WriteLine($"{step + 1}\t{operation}\tlistener={index}\troute={route}\tvalue={value}");

                    switch (operation)
                    {
                        case 0:
                            Connect(listener);
                            break;
                        case 1:
                        case 2:
                            ConnectIfNeeded(listener);
                            Register(listener, route, random.Next(2) == 0);
                            break;
                        case 3:
                            hub.Disconnect(listener.Key);
                            if (listener.Builder != null) listener.StaleBuilder = listener.Builder;
                            listener.Builder = null;
                            Array.Clear(listener.Registered, 0, listener.Registered.Length);
                            if (listener.StaleBuilder != null)
                                Assert.Throws<InvalidOperationException>(
                                    () => listener.StaleBuilder.OnUpdated((Action)null));
                            break;
                        case 4:
                            Publish(value, false);
                            break;
                        case 5:
                            using (hub.Sleep()) Publish(value, true);
                            Assert.That(hub.IsAwake, Is.True);
                            break;
                        case 6:
                            ConnectIfNeeded(listener);
                            Assert.Throws<ArgumentNullException>(() =>
                                listener.Builder.OnUpdatedWhere(null, listener.Callbacks[0]));
                            Assert.Throws<ArgumentNullException>(() =>
                                ((IEventHubBuilder)listener.Builder).OnUpdatedWhere(null, listener.ObjectCallback));
                            Assert.Throws<ArgumentException>(() =>
                                listener.Builder.OnUpdatedForFlags((Changes)0, (Action)null));
                            break;
                        case 7:
                            DisposeAndRestart();
                            break;
                    }

                    var connected = 0;
                    foreach (var current in listeners)
                    {
                        var active = current.Builder != null;
                        Assert.That(hub.HasListener(current.Key), Is.EqualTo(active));
                        if (active) connected++;
                    }

                    Assert.That(hub.ConnectionCount, Is.EqualTo(connected));
                    Publish(value, false);
                    reachedDepth++;
                }

                DisposeAndRestart();
            }
            finally
            {
                hub.Dispose();
                stopwatch.Stop();
                var elapsed = stopwatch.Elapsed.TotalMilliseconds;
                TestContext.WriteLine(FormattableString.Invariant(
                    $"EventHub continuous seed={seed} depth={reachedDepth}/{depth} elapsedMs={elapsed:F3}"));
            }

            Assert.That(reachedDepth, Is.EqualTo(depth));

            void ConnectIfNeeded(Listener listener)
            {
                if (listener.Builder == null) Connect(listener);
            }

            void Connect(Listener listener)
            {
                if (listener.Builder != null)
                {
                    Assert.Throws<InvalidOperationException>(() => hub.Connect(listener.Key));
                    return;
                }

                listener.Builder = hub.Connect(listener.Key)
                    .OnDisposing(() => disposing++)
                    .OnDisposed(() => disposed++);
                if (listener.StaleBuilder != null)
                    Assert.Throws<InvalidOperationException>(() => listener.StaleBuilder.OnUpdated((Action)null));
            }

            void Register(Listener listener, Route route, bool alternateMask)
            {
                var routeIndex = (int)route;
                var callback = listener.Callbacks[routeIndex];
                IEventHubBuilder returned = null;
                TestDelegate register = () =>
                {
                    switch (route)
                    {
                        case Route.Root:
                            returned = listener.Builder.OnUpdated(callback);
                            break;
                        case Route.Target:
                            returned = listener.Builder.OnUpdatedWhen(1UL, callback);
                            break;
                        case Route.Predicate:
                            returned = listener.Builder.OnUpdatedWhere(predicate, callback);
                            break;
                        case Route.Flags:
                            returned = alternateMask
                                ? listener.Builder.OnUpdatedForFlags(AlternateChanges.Shape, callback)
                                : listener.Builder.OnUpdatedForFlags(Changes.Shape, callback);
                            break;
                        case Route.CombinedPredicate:
                            returned = listener.Builder.OnUpdatedWhere(combinedPredicate, callback);
                            break;
                        case Route.Object:
                            returned = ((IEventHubBuilder)listener.Builder)
                                .OnUpdatedWhere(objectPredicate, listener.ObjectCallback);
                            break;
                        case Route.Parameterless:
                            returned = alternateMask
                                ? listener.Builder.OnUpdatedForFlags(
                                    AlternateChanges.Location,
                                    listener.ParameterlessCallback)
                                : listener.Builder.OnUpdatedForFlags(Changes.Location, listener.ParameterlessCallback);
                            break;
                    }
                };

                if (listener.Registered[routeIndex]) Assert.Throws<InvalidOperationException>(register);
                else
                {
                    register();
                    listener.Registered[routeIndex] = true;
                    Assert.That(returned, Is.SameAs(listener.Builder));
                }
            }

            void Publish(ulong value, bool sleeping)
            {
                received.Clear();
                var expected = new List<string>();

                if (!sleeping)
                {
                    for (int index = 0; index < listeners.Length; index++)
                    {
                        var listener = listeners[index];

                        for (int routeIndex = 0; routeIndex < listener.Registered.Length; routeIndex++)
                        {
                            if (!listener.Registered[routeIndex]) continue;

                            var route = (Route)routeIndex;
                            var matches = route switch
                            {
                                Route.Root => true,
                                Route.Target => value == 1,
                                Route.Predicate => value > 2,
                                Route.Flags => (value & 1) != 0,
                                Route.CombinedPredicate => (value & 1) != 0 && value > 2,
                                Route.Object => value < 4,
                                Route.Parameterless => (value & 2) != 0,
                                _ => false
                            };

                            if (matches)
                            {
                                expected.Add(route == Route.Parameterless
                                    ? $"{index}:{routeIndex}"
                                    : $"{index}:{routeIndex}:{value}");
                            }
                        }
                    }
                }

                hub.Update(value);
                Assert.That(received, Is.EquivalentTo(expected));
            }

            void DisposeAndRestart()
            {
                var connected = hub.ConnectionCount;
                var previousDisposing = disposing;
                var previousDisposed = disposed;
                hub.Dispose();
                Assert.That(disposing - previousDisposing, Is.EqualTo(connected));
                Assert.That(disposed - previousDisposed, Is.EqualTo(connected));
                Assert.That(hub.IsDisposed, Is.True);
                Assert.That(hub.ConnectionCount, Is.Zero);

                foreach (var listener in listeners)
                {
                    var builder = listener.Builder ?? listener.StaleBuilder;
                    if (builder != null)
                    {
                        Assert.Throws<ObjectDisposedException>(() =>
                            builder.OnUpdatedForFlags((Changes)0, (Action)null));
                    }

                    listener.Builder = null;
                    listener.StaleBuilder = null;
                    Array.Clear(listener.Registered, 0, listener.Registered.Length);
                }

                hub = new EventHub<ulong>(new object());
            }
        }
    }
}
