using System;
using System.Diagnostics;
using System.Threading;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Samples
{
    public static class BootstrapperSnippet
    {
        // Content
        public static void RunHost()
        {
            // Initialize once per process, before the host loop starts.
            Bootstrapper.Initialize(
                logger: (message, level) => Console.WriteLine($"[{level}] {message}"),
                exceptionHandler: exception => Console.Error.WriteLine(exception),
                applicationId: "MyGame");

            var ready = Bootstrapper.IsInitialized; // true
            var update = EventStream.Main.Subscribe(deltaTime => Console.WriteLine($"Tick: {deltaTime:0.000}s"));

            // The host computes the elapsed seconds of each frame and publishes them through EventStream.Main.
            var clock = Stopwatch.StartNew();
            var previous = clock.Elapsed.TotalSeconds;
            try
            {
                for (int frame = 0; frame < 3; frame++)
                {
                    Thread.Sleep(16);
                    var current = clock.Elapsed.TotalSeconds;
                    EventStream.Main.OnNext((float)(current - previous));
                    previous = current;
                }
            }
            finally
            {
                // The host disposes the main stream once, when it shuts down.
                update.Dispose();
                EventStream.Main.Dispose();
            }
        }

        public static void RejectSecondInitialization()
        {
            try
            {
                Bootstrapper.Initialize();
            }
            catch (InvalidOperationException)
            {
                // Bootstrapping can start only once per process, whether the first attempt succeeded or failed.
            }
        }
    }
}
