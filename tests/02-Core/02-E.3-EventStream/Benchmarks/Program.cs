using System;
using System.Diagnostics;
using System.Globalization;

namespace BlackThunder.UniEngine.Benchmarks
{
    // Measures one scenario per process, so that neither tiered compilation nor core scheduling
    // carries over between scenarios. run.ps1 starts this once for every scenario and build.
    // Usage: EventStreamBenchmarks (list | quick | <scenario> [affinity mask])
    public static class Program
    {
        // Internal
        private const double WarmupSeconds = 1.5;
        private const double RoundSeconds = 0.5;
        private const int RoundCount = 5;
        private const double TargetBatchSeconds = 0.0002;


        // Content --------------------
        // Entry
        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "list")
            {
                foreach (var name in Scenarios.All.Keys) Console.WriteLine(name);
                return 0;
            }

            if (args[0] == "quick")
            {
                foreach (var name in Scenarios.Quick) Console.WriteLine(name);
                return 0;
            }

            if (!Scenarios.All.TryGetValue(args[0], out var create))
            {
                Console.Error.WriteLine($"Unknown scenario: {args[0]}");
                return 1;
            }

            if (args.Length > 1) Pin(args[1]);

            var memoryBefore = GC.GetTotalMemory(true);
            var start = Stopwatch.GetTimestamp();
            var scenario = create();
            var buildSeconds = Seconds(start);
            var memory = GC.GetTotalMemory(true) - memoryBefore;

            Measure(args[0], scenario, buildSeconds, memory);
            GC.KeepAlive(scenario.KeepAlive);
            return 0;
        }

        // Pins the process to the given cores. A hybrid CPU otherwise moves it between core types.
        private static void Pin(string mask)
        {
            var value = mask.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? long.Parse(mask.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : long.Parse(mask, CultureInfo.InvariantCulture);

            using var process = Process.GetCurrentProcess();
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                process.ProcessorAffinity = (IntPtr)value;
            else
                Console.Error.WriteLine("Pinning to cores is not supported on this platform.");

            process.PriorityClass = ProcessPriorityClass.High;
        }

        // Measurement
        private static void Measure(string name, Scenario scenario, double buildSeconds, long memory)
        {
            var batch = 1;
            var start = Stopwatch.GetTimestamp();
            while (Seconds(start) < WarmupSeconds)
            {
                var batchStart = Stopwatch.GetTimestamp();
                Run(scenario.Operation, batch);
                if (Seconds(batchStart) < TargetBatchSeconds && batch < 1 << 20) batch *= 2;
            }

            var times = new double[RoundCount];
            var bytes = new double[RoundCount];
            for (int round = 0; round < RoundCount; round++)
            {
                Scenarios.Calls = 0;
                long operations = 0;
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                start = Stopwatch.GetTimestamp();

                while (Seconds(start) < RoundSeconds)
                {
                    Run(scenario.Operation, batch);
                    operations += batch;
                }

                var elapsed = Seconds(start);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

                if (Scenarios.Calls != scenario.ExpectedCalls * operations)
                    throw new InvalidOperationException(
                        $"{name}: {Scenarios.Calls} calls, expected {scenario.ExpectedCalls * operations}.");

                times[round] = elapsed * 1e9 / operations;
                bytes[round] = (double)allocated / operations;
            }

            Array.Sort(times);
            Array.Sort(bytes);

            // name, median ns, fastest ns, slowest ns, bytes per operation, build ms, bytes kept by the scenario
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1:F1},{2:F1},{3:F1},{4:F1},{5:F0},{6}",
                name,
                times[RoundCount / 2],
                times[0],
                times[RoundCount - 1],
                bytes[RoundCount / 2],
                buildSeconds * 1000,
                memory));
        }

        private static void Run(Action operation, int count)
        {
            for (int i = 0; i < count; i++) operation();
        }

        private static double Seconds(long start) =>
            (double)(Stopwatch.GetTimestamp() - start) / Stopwatch.Frequency;
    }
}
