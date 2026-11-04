using System;
using System.Diagnostics;
using Blackthunder.UniEngine.Core;
#if !EVENTSTREAM_STANDALONE
using NUnit.Framework;
#endif

namespace BlackThunder.UniEngine.Tests
{
#if !EVENTSTREAM_STANDALONE
    [NonParallelizable]
#endif
    public class EventStreamBenchmarks
    {
        // Internal
        private const int TargetCount = 16;
        private const int SubscriberCount = 64;
        private const int WarmupCount = 10000;
        private const int PublicationCount = 200000;
        private const int RoundCount = 3;
        private const uint OneBit = 1;
        private const uint FifteenBits = 0x7fff;
        private const uint AllBits = 0xffff;
        private static readonly bool _recordsAllocation = RecordsAllocation();

#if !EVENTSTREAM_LEGACY
        private static readonly Filter<uint>[] _sharedFilters = CreateSharedFilters();
#endif


        // Content --------------------
        // Scenarios
#if !EVENTSTREAM_STANDALONE
        [Test, Explicit, Category("Benchmark")]
#endif
        public void ComparePropagationAndSubscriptionChurn()
        {
#if DEBUG
            throw new InvalidOperationException("Run this benchmark in Release.");
#endif
#if EVENTSTREAM_LEGACY
            using var router = EventRouterExtensions.Route<uint>(static value => value);
            var subscribers = new Subscribers();
            for (var i = 0; i < SubscriberCount; i++)
                router.Subscribe(1u << (i % TargetCount), subscribers.Permanent[i]);
            Measure("A Router", subscribers, _ => router.OnNext(OneBit), OneBit);
            MeasureWhere("B Separate Where", OneBit, separate: true);
            MeasureWhere("C Shared Where", OneBit);
            MeasureDirect("D Direct matching four", OneBit);
            MeasureWhere("I-C 60 of 64", FifteenBits);
            MeasureDirect("J-C Direct 64", AllBits);
            MeasureWhere("K-C Common rejection", OneBit, rejectCommon: true);
            MeasureWhere("H-C Shared Where churn", OneBit, churn: true);
#else
            MeasureFilters("E Shared filters", OneBit);
            MeasureFilters("F Separate filters", OneBit, separate: true);
            MeasureFilters("G Common acceptance", OneBit, common: true);
            MeasureFilters("I-E 60 of 64", FifteenBits);
            MeasureDirect("J-E Direct 64", AllBits);
            MeasureFilters("K-E Common rejection", OneBit, common: true, rejectCommon: true);
            MeasureFilters("H-E Shared filter churn", OneBit, churn: true);
            MeasureSourceChurn();
#endif
        }

#if EVENTSTREAM_LEGACY
        // Build this mode against the archived pre-migration sources.
        private static void MeasureWhere(string name, uint value, bool separate = false, bool rejectCommon = false, bool churn = false)
        {
            using var stream = new EventStream<uint>();
            var subscribers = new Subscribers();
            IEventStream<uint> parent = rejectCommon ? stream.Where(static _ => false) : stream;
            var count = separate ? SubscriberCount : TargetCount;
            var paths = new IEventStream<uint>[count];
            for (var i = 0; i < count; i++)
                paths[i] = parent.Where(MatchTarget(i % TargetCount));
            for (var i = 0; i < SubscriberCount; i++)
                paths[i % count].Subscribe(subscribers.Permanent[i]);
            if (churn)
                MeasureChurn(name, subscribers, (bit, callback) => paths[bit].Subscribe(callback), stream.OnNext);
            else
                Measure(name, subscribers, _ => stream.OnNext(value), rejectCommon ? 0u : value);
        }
#else
        private static void MeasureFilters(string name, uint value, bool separate = false, bool common = false,
            bool rejectCommon = false, bool churn = false)
        {
            using var stream = new EventStream<uint>();
            var subscribers = new Subscribers();
            var prefix = new Filter<uint>("Common", v => !rejectCommon && v != 0);
            for (var i = 0; i < SubscriberCount; i++)
            {
                var bit = i % TargetCount;
                var filter = separate ? CreateFilter(bit) : _sharedFilters[bit];
                var path = common ? stream.Where(prefix).Where(filter) : stream.Where(filter);
                path.Subscribe(subscribers.Permanent[i]);
            }
            if (churn)
                MeasureChurn(name, subscribers, (bit, callback) => stream.Where(_sharedFilters[bit]).Subscribe(callback), stream.OnNext);
            else
                Measure(name, subscribers, _ => stream.OnNext(value), rejectCommon ? 0u : value);
        }

        private static Filter<uint> CreateFilter(int bit) => new($"Bit {bit}", MatchTarget(bit));

        private static Filter<uint>[] CreateSharedFilters()
        {
            var filters = new Filter<uint>[TargetCount];
            for (var i = 0; i < filters.Length; i++) filters[i] = CreateFilter(i);
            return filters;
        }

        private static void MeasureSourceChurn()
        {
            using var source = new EventSource<uint>();
            var children = new EventSource<uint>[TargetCount];
            var subscribers = new Subscribers();
            try
            {
                for (var bit = 0; bit < children.Length; bit++)
                {
                    var child = new EventSource<uint>();
                    children[bit] = child;
                    var matches = MatchTarget(bit);
                    source.Subscribe(value =>
                    {
                        if (matches(value)) child.OnNext(value);
                    }, child.OnCompleted, child.OnError);
                }
                for (var i = 0; i < SubscriberCount; i++)
                    children[i % TargetCount].Subscribe(subscribers.Permanent[i]);
                MeasureChurn("H-S Shared source churn", subscribers,
                    (bit, callback) => children[bit].Subscribe(callback), source.OnNext);
            }
            finally
            {
                foreach (var child in children) child?.Dispose();
            }
        }
#endif

        private static Func<uint, bool> MatchTarget(int bit)
        {
            var mask = 1u << bit;
            return value => (value & mask) != 0;
        }

        private static void MeasureDirect(string name, uint activeMask)
        {
            using var stream = new EventStream<uint>();
            var subscribers = new Subscribers();
            for (var i = 0; i < SubscriberCount; i++)
                if ((activeMask & (1u << (i % TargetCount))) != 0)
                    stream.Subscribe(subscribers.Permanent[i]);
            Measure(name, subscribers, _ => stream.OnNext(OneBit), activeMask);
        }

        // Measurement
        private static void MeasureChurn(string name, Subscribers subscribers,
            Func<int, Action<uint>, IDisposable> subscribe, Action<uint> publish)
        {
            var handles = new IDisposable[TargetCount];
            Measure(name, subscribers, frame =>
            {
                for (var slot = 0; slot < handles.Length; slot++)
                    handles[slot] = subscribe((frame + slot) % TargetCount, subscribers.Temporary[slot]);
                publish(OneBit);
                for (var slot = 0; slot < handles.Length; slot++) handles[slot].Dispose();
            }, OneBit, true);
        }

        private static void Measure(string name, Subscribers subscribers, Action<int> publish, uint activeMask, bool churn = false)
        {
            var start = Stopwatch.GetTimestamp();
            do
            {
                Run(publish, WarmupCount);
                subscribers.VerifyAndReset(WarmupCount, activeMask, churn);
            }
            while (Stopwatch.GetTimestamp() - start < Stopwatch.Frequency * 1.5);

            var times = new double[RoundCount];
            var allocations = new double[RoundCount];
            var unit = churn ? "frame" : "publication";
            for (var round = 0; round < RoundCount; round++)
            {
                var before = _recordsAllocation ? GC.GetAllocatedBytesForCurrentThread() : 0;
                start = Stopwatch.GetTimestamp();
                Run(publish, PublicationCount);
                var end = Stopwatch.GetTimestamp();
                var allocated = _recordsAllocation ? GC.GetAllocatedBytesForCurrentThread() - before : 0;

                subscribers.VerifyAndReset(PublicationCount, activeMask, churn);
                times[round] = (end - start) * 1000000.0 / Stopwatch.Frequency / PublicationCount;
                allocations[round] = (double)allocated / PublicationCount;
                Write(FormattableString.Invariant(
                    $"{name} round {round + 1}: {times[round]:F4} us/{unit}, {Allocation(allocations[round], unit)}."));
            }

            Array.Sort(times);
            Array.Sort(allocations);
            Write(FormattableString.Invariant(
                $"{name} median: {times[RoundCount / 2]:F4} us/{unit}, {Allocation(allocations[RoundCount / 2], unit)}."));
        }

        private static bool RecordsAllocation()
        {
#if ENABLE_IL2CPP
            // Unity's IL2CPP player does not implement the per-thread allocation counter.
            return false;
#else
            var before = GC.GetAllocatedBytesForCurrentThread();
            var payload = new byte[32768];
            var after = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(payload);
            return after - before >= payload.Length;
#endif
        }

        private static string Allocation(double bytes, string unit) => _recordsAllocation
            ? FormattableString.Invariant($"{bytes:F4} B/{unit}") : "allocation counter unavailable";

        private static void Run(Action<int> publish, int count)
        {
            for (var i = 0; i < count; i++) publish(i);
        }

        private static void Write(string text)
        {
#if EVENTSTREAM_STANDALONE
            Console.WriteLine(text);
#else
            TestContext.Out.WriteLine(text);
#endif
        }

        // Verification
        private class Subscribers
        {
            public Action<uint>[] Permanent { get; }
            public Action<uint>[] Temporary { get; }
            private readonly int[] _permanentCalls = new int[SubscriberCount];
            private readonly int[] _temporaryCalls = new int[TargetCount];

            public Subscribers()
            {
                Permanent = CreateCallbacks(_permanentCalls);
                Temporary = CreateCallbacks(_temporaryCalls);
            }

            private static Action<uint>[] CreateCallbacks(int[] counts)
            {
                var callbacks = new Action<uint>[counts.Length];
                for (var i = 0; i < callbacks.Length; i++)
                {
                    var subscriber = i;
                    callbacks[i] = _ => counts[subscriber]++;
                }
                return callbacks;
            }

            public void VerifyAndReset(int publications, uint activeMask, bool churn)
            {
                for (var i = 0; i < _permanentCalls.Length; i++)
                {
                    var expected = (activeMask & (1u << (i % TargetCount))) == 0 ? 0 : publications;
                    if (_permanentCalls[i] != expected)
                        throw new InvalidOperationException($"Permanent {i}: {_permanentCalls[i]} calls, expected {expected}.");
                }
                var expectedTemporary = churn ? publications / TargetCount : 0;
                for (var i = 0; i < _temporaryCalls.Length; i++)
                    if (_temporaryCalls[i] != expectedTemporary)
                        throw new InvalidOperationException($"Temporary {i}: {_temporaryCalls[i]} calls, expected {expectedTemporary}.");
                Array.Clear(_permanentCalls, 0, _permanentCalls.Length);
                Array.Clear(_temporaryCalls, 0, _temporaryCalls.Length);
            }
        }
    }
}
