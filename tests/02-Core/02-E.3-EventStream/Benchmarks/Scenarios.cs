using System;
using System.Collections.Generic;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Benchmarks
{
    // One measured operation and the number of callbacks a single run of it must make.
    internal readonly struct Scenario
    {
        // Front
        internal long ExpectedCalls { get; }
        internal Action Operation { get; }
        internal object KeepAlive { get; }


        // Content
        internal Scenario(long expectedCalls, Action operation, object keepAlive)
        {
            ExpectedCalls = expectedCalls;
            Operation = operation;
            KeepAlive = keepAlive;
        }
    }

    // Each scenario compares individual traversal, where every subscriber checks its own condition,
    // with the pipeline, where subscribers share condition nodes. Only the public API is used,
    // so the same scenarios build against an earlier commit of the library.
    internal static class Scenarios
    {
        // Front
        internal static long Calls;

        internal static IReadOnlyList<string> Quick { get; } = new[]
        {
            "A-Pipeline-64",
            "B-Pipeline-64of64",
            "D-Pipeline-Rejected",
            "E-Pipeline-NewNode",
            "F-Take",
            "R-Pipeline-1000000",
        };

        internal static IReadOnlyDictionary<string, Func<Scenario>> All { get; } = Create();

        // Internal
        private const int Passing = 0;
        private const int Rejecting = -1;


        // Content --------------------
        // Catalog
        private static Dictionary<string, Func<Scenario>> Create()
        {
            var scenarios = new Dictionary<string, Func<Scenario>>();

            // A: the number of subscribers grows while 1 in 16 receives the value.
            foreach (var count in new[] { 16, 64, 256, 1000, 1024 })
            {
                var subscribers = count;
                scenarios[$"A-Individual-{count}"] = () => Individual(subscribers, 16, 1);
                scenarios[$"A-Pipeline-{count}"] = () => Pipeline(subscribers, 16, 1);
            }

            // B: the share of receivers grows with 64 subscribers and 16 conditions.
            foreach (var passing in new[] { 2, 8, 15, 16 })
            {
                var kinds = passing;
                scenarios[$"B-Individual-{kinds * 4}of64"] = () => Individual(64, 16, kinds);
                scenarios[$"B-Pipeline-{kinds * 4}of64"] = () => Pipeline(64, 16, kinds);
            }
            scenarios["B-Direct-64of64"] = Direct;

            // C: 4 of 64 subscribers receive while the number of shared conditions changes.
            scenarios["C-Pipeline-Kinds2"] = () => Shared(2);
            scenarios["C-Pipeline-Kinds16"] = () => Shared(16);
            scenarios["C-Pipeline-Kinds64"] = () => Shared(64);

            // D: a common condition sits in front of 16 conditions.
            scenarios["D-Individual-Rejected"] = () => Prefixed(false, false);
            scenarios["D-Pipeline-Rejected"] = () => Prefixed(true, false);
            scenarios["D-Individual-Accepted"] = () => Prefixed(false, true);
            scenarios["D-Pipeline-Accepted"] = () => Prefixed(true, true);

            // E: one subscribe and release with 64 subscribers in place.
            scenarios["E-Individual-Subscribe"] = SubscribeIndividual;
            scenarios["E-Pipeline-ExistingNode"] = () => SubscribePipeline(existingNode: true);
            scenarios["E-Pipeline-NewNode"] = () => SubscribePipeline(existingNode: false);
            scenarios["E-Pipeline-MethodGroup"] = SubscribeMethodGroup;
            scenarios["E-Pipeline-Branch"] = () => SubscribeRelation(path => path.Branch());
            scenarios["E-Pipeline-Take"] = () => SubscribeRelation(path => path.Take(1));

            // F: values reach 64 subscribers through a stream made by an operator.
            scenarios["F-Branch"] = () => Through(source => source.Branch());
            scenarios["F-Take"] = () => Through(source => source.Take(int.MaxValue));
            scenarios["F-TakeWhile"] = () => Through(source => source.TakeWhile(_ => true));
            scenarios["F-Skip"] = () => Through(source => source.Skip(0));
            scenarios["F-SkipWhile"] = () => Through(source => source.SkipWhile(_ => false));

            // L: 1 in 16 receives. R: only 4 receive. K: one condition per 4 subscribers, 4 receive.
            // T: subscribe and release one subscription on the L graph.
            scenarios["K-Individual-1000"] = () => Scale("K", true, 1000);
            scenarios["K-Pipeline-1000"] = () => Scale("K", false, 1000);
            foreach (var family in new[] { "L", "R", "K", "T" })
            {
                foreach (var count in new[] { 10_000, 100_000, 1_000_000 })
                {
                    var name = family;
                    var subscribers = count;
                    scenarios[$"{family}-Individual-{count}"] = () => Scale(name, true, subscribers);
                    scenarios[$"{family}-Pipeline-{count}"] = () => Scale(name, false, subscribers);
                }
            }

            return scenarios;
        }

        // Conditions
        // Separate methods keep a shared call site from seeing one target it could inline away.
        private static Func<int, bool> Condition(int method, int target) =>
            (method % 16) switch
            {
                0 => value => value == target,
                1 => value => value + 1 == target + 1,
                2 => value => value + 2 == target + 2,
                3 => value => value + 3 == target + 3,
                4 => value => value + 4 == target + 4,
                5 => value => value + 5 == target + 5,
                6 => value => value + 6 == target + 6,
                7 => value => value + 7 == target + 7,
                8 => value => value + 8 == target + 8,
                9 => value => value + 9 == target + 9,
                10 => value => value + 10 == target + 10,
                11 => value => value + 11 == target + 11,
                12 => value => value + 12 == target + 12,
                13 => value => value + 13 == target + 13,
                14 => value => value + 14 == target + 14,
                _ => value => value + 15 == target + 15
            };

        private static Func<int, bool>[] CreateConditions(int kinds, int passingKinds)
        {
            var conditions = new Func<int, bool>[kinds];
            for (int i = 0; i < kinds; i++)
                conditions[i] = Condition(i, i < passingKinds ? Passing : Rejecting);

            return conditions;
        }

        private static bool Named(int value) => value == Rejecting;

        // Propagation
        private static Scenario Direct()
        {
            var stream = new EventStream<int>();
            for (int i = 0; i < 64; i++) stream.Subscribe(_ => Calls++);

            return new Scenario(64, () => stream.OnNext(0), stream);
        }

        private static Scenario Individual(int subscribers, int kinds, int passingKinds)
        {
            var stream = new EventStream<int>();
            var conditions = CreateConditions(kinds, passingKinds);
            long expected = 0;

            for (int i = 0; i < subscribers; i++)
            {
                var kind = i % kinds;
                var condition = conditions[kind];
                if (kind < passingKinds) expected++;

                stream.Subscribe(value =>
                {
                    if (condition(value)) Calls++;
                });
            }

            return new Scenario(expected, () => stream.OnNext(0), stream);
        }

        private static Scenario Pipeline(int subscribers, int kinds, int passingKinds)
        {
            var stream = new EventStream<int>();
            var conditions = CreateConditions(kinds, passingKinds);
            long expected = 0;

            for (int i = 0; i < subscribers; i++)
            {
                var kind = i % kinds;
                if (kind < passingKinds) expected++;

                stream.Where(conditions[kind]).Subscribe(_ => Calls++);
            }

            return new Scenario(expected, () => stream.OnNext(0), stream);
        }

        // Four subscribers always receive the value. The rest are spread over the remaining conditions.
        private static Scenario Shared(int kinds)
        {
            const int subscribers = 64;
            const int receivers = 4;
            var stream = new EventStream<int>();
            var unshared = kinds == subscribers;
            var conditions = CreateConditions(kinds, unshared ? receivers : 1);

            for (int i = 0; i < subscribers; i++)
            {
                var index = unshared ? i : i < receivers ? 0 : 1 + (i - receivers) % (kinds - 1);
                stream.Where(conditions[index]).Subscribe(_ => Calls++);
            }

            return new Scenario(receivers, () => stream.OnNext(0), stream);
        }

        private static Scenario Prefixed(bool pipeline, bool accepted)
        {
            const int subscribers = 64;
            const int kinds = 16;
            var stream = new EventStream<int>();
            Func<int, bool> common = value => value != (accepted ? Rejecting : Passing);
            var conditions = CreateConditions(kinds, 1);

            for (int i = 0; i < subscribers; i++)
            {
                var condition = conditions[i % kinds];
                if (pipeline)
                {
                    stream.Where(common).Where(condition).Subscribe(_ => Calls++);
                    continue;
                }

                stream.Subscribe(value =>
                {
                    if (common(value) && condition(value)) Calls++;
                });
            }

            return new Scenario(accepted ? subscribers / kinds : 0, () => stream.OnNext(0), stream);
        }

        private static Scenario Through(Func<EventStream<int>, EventStream<int>> create)
        {
            var source = new EventStream<int>();
            var relation = create(source);
            for (int i = 0; i < 64; i++) relation.Subscribe(_ => Calls++);

            return new Scenario(64, () => source.OnNext(0), source);
        }

        // Subscription
        private static Scenario SubscribeIndividual()
        {
            var stream = new EventStream<int>();
            var conditions = CreateConditions(16, 1);

            for (int i = 0; i < 64; i++)
            {
                var condition = conditions[i % 16];
                stream.Subscribe(value =>
                {
                    if (condition(value)) Calls++;
                });
            }

            var first = conditions[0];
            Action<int> added = value =>
            {
                if (first(value)) Calls++;
            };

            return new Scenario(0, () => stream.Subscribe(added).Dispose(), stream);
        }

        private static Scenario SubscribePipeline(bool existingNode)
        {
            var stream = new EventStream<int>();
            var conditions = CreateConditions(16, 1);
            for (int i = 0; i < 64; i++) stream.Where(conditions[i % 16]).Subscribe(_ => Calls++);

            var target = existingNode ? conditions[0] : Condition(0, Passing);
            Action<int> added = _ => Calls++;

            return new Scenario(0, () => stream.Where(target).Subscribe(added).Dispose(), stream);
        }

        // Passing a method group makes a new delegate on every call.
        private static Scenario SubscribeMethodGroup()
        {
            var stream = new EventStream<int>();
            var conditions = CreateConditions(16, 1);
            for (int i = 0; i < 64; i++) stream.Where(conditions[i % 16]).Subscribe(_ => Calls++);
            stream.Where(Named).Subscribe(_ => Calls++);

            Action<int> added = _ => Calls++;

            return new Scenario(0, () => stream.Where(Named).Subscribe(added).Dispose(), stream);
        }

        private static Scenario SubscribeRelation(Func<EventPath<int>, EventStream<int>> create)
        {
            var stream = new EventStream<int>();
            var conditions = CreateConditions(16, 1);
            for (int i = 0; i < 64; i++) stream.Where(conditions[i % 16]).Subscribe(_ => Calls++);

            Action<int> added = _ => Calls++;

            return new Scenario(0, () => create(stream.Where(conditions[0])).Subscribe(added).Dispose(), stream);
        }

        // Scale
        private static Scenario Scale(string family, bool individual, int subscribers)
        {
            var kinds = family == "K" ? subscribers / 4 : 16;
            var conditions = CreateConditions(kinds, 1);
            var stream = new EventStream<int>();
            long expected = 0;

            for (int i = 0; i < subscribers; i++)
            {
                var kind = family == "R" ? (i < 4 ? 0 : 1 + (i - 4) % 15) : i % kinds;
                if (kind == 0) expected++;

                if (individual)
                {
                    var condition = conditions[kind];
                    stream.Subscribe(value =>
                    {
                        if (condition(value)) Calls++;
                    });
                    continue;
                }

                stream.Where(conditions[kind]).Subscribe(_ => Calls++);
            }

            if (family != "T") return new Scenario(expected, () => stream.OnNext(0), stream);

            var first = conditions[0];
            Action<int> added = individual
                ? value =>
                {
                    if (first(value)) Calls++;
                }
                : _ => Calls++;
            Action churn = individual
                ? () => stream.Subscribe(added).Dispose()
                : () => stream.Where(first).Subscribe(added).Dispose();

            return new Scenario(0, churn, stream);
        }
    }
}
