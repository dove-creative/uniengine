using System;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Samples
{
    public static class EventStreamSample
    {
        // Internal
        private enum Phase { Simulate, Render }

        [Flags]
        private enum GeoEvents { Shape = 1 }

        [Flags]
        private enum RouteEvents { Location = 2 }


        // Content
        public static void Run()
        {
            FilterAndMatch();
            OrderSubscribers();
            MatchFlagBits();
            LimitAndBranch();
            MergeStreams();
            ComputeInParallel();

            // Where takes a condition; When matches one value by equality.
            void FilterAndMatch()
            {
                using var spawned = new EventStream<string>();
                using var enemies = spawned.Where(name => name.StartsWith("enemy"))
                    .Subscribe(name => Console.WriteLine($"Where enemy*: {name}"));
                using var boss = spawned.When("boss").Subscribe(_ => Console.WriteLine("When boss: boss music starts"));

                spawned.OnNext("enemy-1");
                spawned.OnNext("ally-1");
                spawned.OnNext("boss");
            }

            // ForOrder runs groups in enum order, whatever the subscription order.
            void OrderSubscribers()
            {
                using var frame = new EventStream<int>();
                using var render = frame.ForOrder(Phase.Render)
                    .Subscribe(index => Console.WriteLine($"Render {index}"));
                using var simulate = frame.ForOrder(Phase.Simulate)
                    .Subscribe(index => Console.WriteLine($"Simulate {index}"));

                frame.OnNext(1);
            }

            // ForFlags passes values that share at least one bit with the mask, even across enum types.
            void MatchFlagBits()
            {
                using var changes = new EventStream<ulong>();
                using var shapes = changes.ForFlags(GeoEvents.Shape).Subscribe(_ => Console.WriteLine("Flags: shape"));
                using var routes = changes.ForFlags(RouteEvents.Location)
                    .Subscribe(_ => Console.WriteLine("Flags: location"));

                changes.OnNext(GeoEvents.Shape.ToBits());
                changes.OnNext(GeoEvents.Shape.ToBits().Combine(RouteEvents.Location.ToBits()));
            }

            // Take shares its remaining count among its subscribers; a branch also accepts its own input.
            void LimitAndBranch()
            {
                using var scores = new EventStream<int>();
                var firstTwo = scores.Take(2);
                using var early = firstTwo.Subscribe(score => Console.WriteLine($"Take early: {score}"));
                scores.OnNext(10);
                using var late = firstTwo.Subscribe(score => Console.WriteLine($"Take late: {score}"));
                scores.OnNext(20);
                scores.OnNext(30);

                using var positive = scores.Where(score => score > 0).Branch();
                using var branchHandle = positive.Subscribe(score => Console.WriteLine($"Branch: {score}"));
                scores.OnNext(-1);
                scores.OnNext(5);
                positive.OnNext(-3);
            }

            // Merge forwards both inputs and completes when every input has completed.
            void MergeStreams()
            {
                using var keyboard = new EventStream<string>();
                using var gamepad = new EventStream<string>();
                using var input = keyboard.Merge(gamepad);
                using var handle = input.Subscribe(
                    key => Console.WriteLine($"Merge: {key}"),
                    () => Console.WriteLine("Merge: all inputs completed"),
                    exception => Console.WriteLine($"Merge failed: {exception.Message}"));

                keyboard.OnNext("Space");
                keyboard.OnCompleted();
                gamepad.OnNext("A");
                gamepad.OnCompleted();
            }

            // Async subscribers in one order group run in parallel; the synchronous one runs after them.
            void ComputeInParallel()
            {
                using var frame = new EventStream<int>();
                var left = 0;
                var right = 0;
                using var leftWork = frame.ForOrderAsync(Phase.Simulate).Subscribe(value => left = value * 2);
                using var rightWork = frame.ForOrderAsync(Phase.Simulate).Subscribe(value => right = value * 3);
                using var apply = frame.ForOrder(Phase.Simulate)
                    .Subscribe(_ => Console.WriteLine($"Parallel results applied: {left} and {right}"));

                frame.OnNext(7);
            }
        }
    }
}
