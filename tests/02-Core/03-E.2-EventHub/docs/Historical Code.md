# EventHub 이전 성능 측정 코드

[테스트 계획](02.%20Plans.md)의 구독 해제 알림 전환 비교에 사용한 실제 측정 코드이다. 아래 코드 블록은 해당 파일 전체를 줄 끝만 LF로 맞춰 보존한 것이다.

비교 기준은 2a66c2d06ed03d44e5566a06c1b84d74821aa497이고, 변경 후 라이브러리는 당시 작업 트리의 사본이다. 원본 프로젝트는 net10.0·C# 9·nullable/implicit usings 비활성화로 빌드했다. 코드의 Where 기반 Builder 호출은 당시 API이며 현재 API용으로 수정하지 않는다. 이 문서는 독립 실행 프로젝트나 재측정 요청이 아니다.

## 측정 프로그램

```csharp
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Blackthunder.UniEngine.Core;

internal static class Program
{
    // Internal
    private const int RoundCount = 9;
    private const double WarmupSeconds = 1.0;
    private const double TargetRoundSeconds = 0.15;

    // Content
    private static void Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        using var process = Process.GetCurrentProcess();
        process.ProcessorAffinity = new IntPtr(0x3);
        process.PriorityClass = ProcessPriorityClass.High;
        Console.WriteLine($"Label={args[0]}; Runtime={RuntimeInformation.FrameworkDescription}; Architecture={RuntimeInformation.ProcessArchitecture}; Affinity=0x{process.ProcessorAffinity.ToInt64():X}; Priority={process.PriorityClass}; StopwatchFrequency={Stopwatch.Frequency}; TieredCompilation={Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")}; TieredPGO={Environment.GetEnvironmentVariable("DOTNET_TieredPGO")}");
        Console.WriteLine("Scenario,Listeners,Iterations,MinNs,MedianNs,MaxNs,AllocatedBytes,RoundNs");

        foreach (var accept in new[] { false, true })
        {
            foreach (var count in new[] { 10, 100, 1000 })
            {
                Measure(count, accept);
            }
        }
    }

    private static void Measure(int count, bool accept)
    {
        using var hub = new EventHub<int>(new object());
        var listeners = new Listener[count];
        Func<int, bool> condition = accept ? AlwaysTrue : AlwaysFalse;

        for (var index = 0; index < count; index++)
        {
            var listener = new Listener();
            listeners[index] = listener;
            hub.Connect(listener).Where(condition).OnUpdated(listener.Receive);
        }

        long totalUpdates = 0;
        var iterations = 1024;
        var warmupStart = Stopwatch.GetTimestamp();

        do
        {
            var batchStart = Stopwatch.GetTimestamp();
            RunBatch(hub, iterations);
            totalUpdates += iterations;
            var seconds = (Stopwatch.GetTimestamp() - batchStart) / (double)Stopwatch.Frequency;

            if (seconds < TargetRoundSeconds / 2 && iterations < 16777216) iterations *= 2;
        }
        while ((Stopwatch.GetTimestamp() - warmupStart) / (double)Stopwatch.Frequency < WarmupSeconds);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var samples = new double[RoundCount];
        long allocations = 0;

        for (var round = 0; round < RoundCount; round++)
        {
            var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            RunBatch(hub, iterations);
            var elapsed = Stopwatch.GetTimestamp() - start;
            allocations += GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
            totalUpdates += iterations;
            samples[round] = elapsed * 1_000_000_000.0 / Stopwatch.Frequency / iterations;
        }

        foreach (var listener in listeners)
        {
            if (listener.Received != (accept ? totalUpdates : 0))
                throw new InvalidOperationException("Incorrect delivery count.");
        }

        var ordered = (double[])samples.Clone();
        Array.Sort(ordered);
        Console.WriteLine($"{(accept ? "AllReceive" : "WhereFalse")},{count},{iterations},{ordered[0]:F3},{ordered[RoundCount / 2]:F3},{ordered[RoundCount - 1]:F3},{allocations},{string.Join(";", Array.ConvertAll(samples, value => value.ToString("F3")))}");
        GC.KeepAlive(listeners);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunBatch(EventHub<int> hub, int iterations)
    {
        for (var index = 0; index < iterations; index++)
        {
            hub.Update(1);
        }
    }

    private static bool AlwaysTrue(int value) => true;
    private static bool AlwaysFalse(int value) => false;

    private class Listener
    {
        public long Received { get; private set; }

        public void Receive(int value) => Received += value;
    }
}
```

[테스트 계획](02.%20Plans.md) · [검증 결과](../../docs/Hub%20Results.md)
