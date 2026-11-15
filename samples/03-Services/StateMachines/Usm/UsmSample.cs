using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.UniEngine.StateMachines.Usm;
using BlackThunder.UniEngine.StateMachines.Usm.Core;
using Parallel = BlackThunder.UniEngine.StateMachines.Usm.Parallel;

namespace BlackThunder.UniEngine.Samples
{
    public static class UsmSample
    {
        // Content --------------------
        // Scenarios
        public static void Run()
        {
            RunMatrixAndSequence();
            RunSelector();
            RunDirect();
            RunLoopAndReframe();
            RunParallel();
        }

        private static void RunMatrixAndSequence()
        {
            Console.WriteLine("[Matrix + Sequence]");
            var matrix = new DecisionMatrixBuilder()
                .FromEntry().GoTo("prepare")
                .From("prepare").When(DetailedOperationStatus.ChildCompleted).With("input-a-b").GoTo("finish")
                .From("finish").When(DetailedOperationStatus.Success).GoTo(null)
                .Build();

            var root = new Node(
                "root",
                transitioner: new MatrixTransitioner(matrix),
                children: new[]
                {
                    new Node("prepare", transitioner: new Sequence(), children: new[]
                    {
                        new Node("first", new AppendWork("-a")),
                        new Node("second", new AppendWork("-b"))
                    }),
                    new Node("finish", new AppendWork("-end"))
                });

            root.Initialize();
            var frame = new FrameContext("input");
            var result = root.Update(frame);
            Console.WriteLine($"Result: {result.DetailedOperationStatus}, payload: {result.Result}");
            Console.WriteLine($"Visited: {string.Join(" -> ", frame.Nexts.Select(next => next.Id))}");
        }

        private static void RunSelector()
        {
            Console.WriteLine("[Selector + StopOnFailure]");
            foreach (var policy in new[] { AbortPolicies.None, AbortPolicies.StopOnFailure })
            {
                var attempt = new CountingWork("attempt", succeeds: false);
                var fallback = new CountingWork("fallback");
                var root = new Node(
                    "selector",
                    transitioner: new Selector(),
                    children: new[]
                    {
                        new Node("attempt", attempt, abortPolicies: policy),
                        new Node("fallback", fallback)
                    });

                root.Initialize();
                var result = root.Update(new FrameContext("input"));
                Console.WriteLine(
                    $"{policy}: {result.DetailedOperationStatus}, "
                    + $"attempt updates: {attempt.Updates}, fallback updates: {fallback.Updates}");

                if (result.IsRunning)
                {
                    root.Update(new FrameContext("input"));
                    Console.WriteLine(
                        $"Next update: attempt updates: {attempt.Updates}, fallback updates: {fallback.Updates}");
                }

                root.Stop(new FrameContext());
            }
        }

        private static void RunDirect()
        {
            Console.WriteLine("[Direct + child-first Stop]");
            var direct = new DirectTransitioner();
            var root = new Node(
                "root",
                new Work().OnStopped(reason => Console.WriteLine($"root stopped: {reason}")),
                direct,
                new[]
                {
                    new Node("idle", new Work()
                        .OnStart(() => Console.WriteLine("idle started"))
                        .OnStopped(reason => Console.WriteLine($"idle stopped: {reason}"))),
                    new Node("move", new Work()
                        .OnStart(() => Console.WriteLine("move started"))
                        .OnStopped(reason => Console.WriteLine($"move stopped: {reason}")))
                });

            root.Initialize();
            direct.SetNext("idle");
            root.Update(new FrameContext());
            direct.SetNext("move");
            root.Update(new FrameContext());
            root.Stop(new FrameContext());
            root.Stop(new FrameContext()); // Repeated Stop does not invoke callbacks again.
        }

        private static void RunLoopAndReframe()
        {
            Console.WriteLine("[LoopType + Reframe]");
            var step = new CountingWork("step");
            var loop = new Node(
                "loop",
                transitioner: new Sequence(LoopType.Conditional),
                children: new[] { new Node("step", step) });

            loop.Initialize();
            for (var tick = 1; tick <= 2; tick++)
            {
                var frame = new FrameContext("input");
                var result = loop.Update(frame);
                Console.WriteLine(
                    $"Conditional tick {tick}: {result.DetailedOperationStatus}, "
                    + $"step updates: {step.Updates}, frame updates: {frame.UpdateCount}");
            }

            loop.Stop(new FrameContext());

            var retry = new CountingWork("retry", updatesToComplete: 3);
            var replay = new Node("replay", retry);
            retry.OnUpdate(() =>
            {
                // Re-register even the same option to request another replay.
                if (retry.Updates < 3) replay.ReframeOption = ReframeOption.Update;
            });

            replay.Initialize();
            var replayFrame = new FrameContext("input", maxNodeUpdates: 8);
            var replayResult = replay.Update(replayFrame);
            Console.WriteLine(
                $"Reframe: {replayResult.DetailedOperationStatus}, starts: {retry.Starts}, "
                + $"work updates: {retry.Updates}, frame updates: {replayFrame.UpdateCount}, "
                + $"pending: {replay.ReframeOption}");
        }

        private static void RunParallel()
        {
            Console.WriteLine("[Parallel]");
            var slow = new CountingWork("slow", updatesToComplete: 2);
            var fast = new CountingWork("fast");
            var all = new Node(
                "all",
                transitioner: new Parallel(ParallelCompletionPolicy.AllSuccess),
                children: new[] { new Node("slow", slow), new Node("fast", fast) });

            all.Initialize();
            var first = all.Update(new FrameContext("input"));
            PrintParallel("AllSuccess tick 1", first);
            PrintParallel("AllSuccess tick 2", all.Update(new FrameContext("input")));
            PrintParallel("Saved tick 1 snapshot", first);
            Console.WriteLine($"Work updates: slow={slow.Updates}, fast={fast.Updates}");

            var any = new Node(
                "any",
                transitioner: new Parallel(ParallelCompletionPolicy.AnySuccess),
                children: new[]
                {
                    new Node("slow", new CountingWork("slow", updatesToComplete: 3)),
                    new Node("fast", new CountingWork("fast")),
                    new Node("unvisited", new CountingWork("unvisited"))
                });

            any.Initialize();
            PrintParallel("AnySuccess", any.Update(new FrameContext("input")));
        }

        private static void PrintParallel(string label, OperationInfo result)
        {
            if (!result.TryGetResult<IReadOnlyList<OperationInfo>>(out var children))
                throw new InvalidOperationException("Parallel must return a child result snapshot.");

            var values = children.Select(child => $"{child.DetailedOperationStatus}({child.Result ?? "null"})");
            Console.WriteLine($"{label}: {result.DetailedOperationStatus} [{string.Join(", ", values)}]");
        }

        // Work definitions
        private class AppendWork : Work
        {
            private readonly string _suffix;


            public AppendWork(string suffix) => _suffix = suffix;

            public override OperationInfo Update(object input) =>
                new OperationInfo(DetailedOperationStatus.Success, input + _suffix);
        }

        private class CountingWork : Work
        {
            // Front
            public int Starts { get; private set; }
            public int Updates { get; private set; }

            // Internal
            private readonly string _name;
            private readonly int _updatesToComplete;
            private readonly bool _succeeds;
            private int _currentUpdates;


            // Content
            public CountingWork(string name, int updatesToComplete = 1, bool succeeds = true)
            {
                _name = name;
                _updatesToComplete = updatesToComplete;
                _succeeds = succeeds;
            }

            public override void Start(object input)
            {
                Starts++;
                _currentUpdates = 0;
                base.Start(input);
            }

            public override OperationInfo Update(object input)
            {
                Updates++;
                _currentUpdates++;
                base.Update(input);
                var status = _currentUpdates < _updatesToComplete
                    ? DetailedOperationStatus.Running
                    : _succeeds ? DetailedOperationStatus.Success : DetailedOperationStatus.Failure;
                return new OperationInfo(status, $"{input}:{_name}");
            }
        }
    }
}

