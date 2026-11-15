using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BlackThunder.UniEngine.Infrastructure;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    internal static class LogScenarios
    {
        // Content
        public static Task RunAsync(string scenario, string applicationId)
        {
            var path = InfrastructureProcess.GetOwnedPath(applicationId);

            if (scenario.StartsWith("Continuous.", StringComparison.Ordinal))
                return LogContinuousScenario.RunAsync(int.Parse(scenario.Substring("Continuous.".Length)), applicationId);

            if (scenario == "InitializationRequired")
            {
                Assert.Throws<InvalidOperationException>(() => Log.Initialize());
                Assert.That(Log.Print(), Is.Empty);
                Log.Write("pending");
                Assert.Throws<InvalidOperationException>(() => Log.Print());
                Log.Reset();
                Assert.That(Log.Print(), Is.Empty);
                Assert.That(Directory.Exists(path), Is.False);
                return Task.CompletedTask;
            }

            Configuration.Initialize(applicationId);
            Log.Initialize((_, _) => { });

            if (scenario.StartsWith("Lifecycle.", StringComparison.Ordinal))
            {
                ExecutePrefix(int.Parse(scenario.Substring("Lifecycle.".Length)), new Action[]
                {
                    () => { Log.Initialize((_, _) => { }); Assert.That(Log.DebugFilePath, Is.Empty); },
                    () => Log.Write("first"),
                    () => Assert.That(Log.Print(), Is.EqualTo("[Log] first" + Environment.NewLine)),
                    () => Assert.That(Log.Print(), Is.Empty),
                    () => Log.Write("discard on initialize"),
                    () =>
                    {
                        Log.Initialize((_, _) => { });
                        Assert.That(Log.Print(), Is.Empty);
                        Assert.That(Log.DebugFilePath, Is.Empty);
                    },
                    () => Log.Write("second"),
                    () => Assert.That(Log.Print(), Is.EqualTo("[Log] second" + Environment.NewLine)),
                });

                return Task.CompletedTask;
            }

            if (scenario.StartsWith("SaveRecovery.", StringComparison.Ordinal))
            {
                SaveRecovery(applicationId, int.Parse(scenario.Substring("SaveRecovery.".Length)));
                return Task.CompletedTask;
            }

            switch (scenario)
            {
                case "DefaultConsole":
                    var originalOutput = Console.Out;
                    using (var output = new StringWriter())
                    {
                        try
                        {
                            Console.SetOut(output);
                            Log.Initialize();
                            Assert.That(Log.Write("console", LogLevel.Warning), Is.EqualTo("console"));
                            Assert.That(output.ToString(), Is.EqualTo("console" + Environment.NewLine));
                        }
                        finally
                        {
                            Console.SetOut(originalOutput);
                        }
                    }

                    break;
                case "MessagesAndLevels":
                    var calls = new List<(string Message, LogLevel Level)>();
                    Log.Initialize((message, level) => calls.Add((message, level)));

                    foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
                        Assert.That(Log.Write("message", level), Is.EqualTo("message"));

                    Assert.That(calls, Is.EqualTo(new[]
                    {
                        ("message", LogLevel.Log), ("message", LogLevel.Warning),
                        ("message", LogLevel.Error), ("message", LogLevel.Exception),
                    }));

                    Log.Write((object)null);
                    Log.Write();
                    Log.WriteLine();
                    Assert.That(
                        Log.Print(),
                        Is.EqualTo(
                            "[Log] message" + Environment.NewLine
                            + "[Warning] message" + Environment.NewLine
                            + "[Error] message" + Environment.NewLine
                            + "[Exception] message" + Environment.NewLine
                            + "[Log] null" + Environment.NewLine
                            + "[Log] Called" + Environment.NewLine + Environment.NewLine));
                    break;
                case "Collections":
                    var numbers = new[] { 1, 2 };
                    Assert.That(Log.Write(numbers), Is.EqualTo("[1, 2]"));
                    Assert.That(Log.Write(numbers, smartWrite: false), Is.EqualTo("System.Int32[]"));
                    Assert.That(Log.Write("text"), Is.EqualTo("text"));
                    Assert.That(Log.WriteCollection<int>(null), Is.EqualTo("null"));
                    Assert.That(Log.WriteCollection(Array.Empty<int>()), Is.EqualTo("[]"));
                    Assert.That(Log.WriteCollection(numbers, number => number * 10), Is.EqualTo("[10, 20]"));
                    var detailed = Log.WriteCollection(numbers, detailedFormat: true);
                    Assert.That(detailed, Does.Contain("Count: 2").And.Contain("[0] 1").And.Contain("[1] 2"));
                    break;
                case "Utf8AndEmptyBuffer":
                    Assert.That(Log.Print(), Is.Empty);
                    Assert.That(Log.DebugFilePath, Is.Empty);
                    Assert.That(Directory.GetFiles(path), Is.Empty);
                    Log.Write("한글 café 🚢");
                    var content = Log.Print();
                    var savedPath = Log.DebugFilePath;
                    Assert.That(content, Is.EqualTo("[Log] 한글 café 🚢" + Environment.NewLine));
                    Assert.That(
                        File.ReadAllBytes(savedPath),
                        Is.EqualTo(new UTF8Encoding(false, true).GetBytes(content)));
                    Assert.That(Log.Print(), Is.Empty);
                    Assert.That(Log.DebugFilePath, Is.EqualTo(savedPath));
                    Assert.That(Directory.GetFiles(path), Has.Length.EqualTo(1));
                    break;
                case "UniqueFiles":
                    var saved = new Dictionary<string, string>();
                    for (int index = 0; index < 5; index++)
                    {
                        Log.Write("message " + index);
                        var text = Log.Print();
                        Assert.That(saved.ContainsKey(Log.DebugFilePath), Is.False);
                        Assert.That(Path.GetDirectoryName(Log.DebugFilePath), Is.EqualTo(path));
                        saved.Add(Log.DebugFilePath, text);
                    }

                    foreach (var pair in saved)
                        Assert.That(File.ReadAllText(pair.Key), Is.EqualTo(pair.Value));

                    Assert.That(Directory.GetFiles(path), Has.Length.EqualTo(5));
                    break;
                case "OutputFailure":
                    var failure = new ApplicationException("output failed");
                    Log.Initialize((_, _) => throw failure);
                    Assert.That(Assert.Throws<ApplicationException>(() => Log.Write("retained")), Is.SameAs(failure));
                    Assert.That(Log.Print(), Is.EqualTo("[Log] retained" + Environment.NewLine));
                    break;
                case "ReentrantOutput":
                    Log.Initialize((message, _) =>
                    {
                        if (message != "outer")
                            return;

                        // A different thread must acquire the buffer lock during the callback.
                        var nested = Task.Run(() => Log.Write("inner"));
                        Assert.That(
                            nested.Wait(TimeSpan.FromSeconds(2)),
                            Is.True,
                            "The output callback retained the buffer lock.");
                    });

                    Log.Write("outer");
                    Assert.That(
                        Log.Print(),
                        Is.EqualTo("[Log] outer" + Environment.NewLine + "[Log] inner" + Environment.NewLine));
                    break;
                case "StackTrace":
                    Log.LogWithStackTrace = true;
                    Log.Write("traced");
                    Assert.That(
                        Log.Print(),
                        Does.StartWith("[Log] traced" + Environment.NewLine).And.Contain(nameof(RunAsync)));
                    Log.LogWithStackTrace = false;
                    Log.Write("plain");
                    Assert.That(Log.Print(), Is.EqualTo("[Log] plain" + Environment.NewLine));
                    break;
                case "ResetAndReinitialize":
                    Log.Write("discarded");
                    Log.Reset();
                    Assert.That(Log.Print(), Is.Empty);
                    Assert.That(Directory.GetFiles(path), Is.Empty);
                    Log.Write("saved");
                    Log.Print();
                    var previousPath = Log.DebugFilePath;
                    Log.Write("pending");
                    var replacementCalls = new List<string>();
                    Log.Initialize((message, _) => replacementCalls.Add(message));
                    Assert.That(Log.DebugFilePath, Is.Empty);
                    Assert.That(Log.Print(), Is.Empty);
                    Assert.That(File.Exists(previousPath), Is.True);
                    Log.Write("replacement");
                    Assert.That(replacementCalls, Is.EqualTo(new[] { "replacement" }));
                    Assert.That(Log.Print(), Is.EqualTo("[Log] replacement" + Environment.NewLine));
                    break;
                case "SaveFailure":
                    Log.Write("retained");
                    Directory.Delete(path);
                    InfrastructureProcess.BlockDataPath(applicationId);
                    Assert.Catch<IOException>(() => Log.Print());
                    Assert.That(Log.DebugFilePath, Is.Empty);
                    Assert.That(File.ReadAllText(path), Is.EqualTo("test-owned path blocker"));
                    break;
                default:
                    throw new ArgumentException("Unknown Log scenario: " + scenario);
            }

            return Task.CompletedTask;
        }

        private static void SaveRecovery(string applicationId, int stepCount)
        {
            var path = InfrastructureProcess.GetOwnedPath(applicationId);
            string previousPath = null;

            ExecutePrefix(stepCount, new Action[]
            {
                () => { Log.Write("previous"); Log.Print(); previousPath = Log.DebugFilePath; },
                () =>
                {
                    File.Delete(previousPath);
                    Directory.Delete(path);
                    InfrastructureProcess.BlockDataPath(applicationId);
                    Log.Write("retained");
                },
                () =>
                {
                    Assert.Catch<IOException>(() => Log.Print());
                    Assert.That(Log.DebugFilePath, Is.EqualTo(previousPath));
                },
                () => { File.Delete(path); Directory.CreateDirectory(path); },
                () =>
                {
                    Assert.That(Log.Print(), Is.EqualTo("[Log] retained" + Environment.NewLine));
                    Assert.That(
                        File.ReadAllText(Log.DebugFilePath),
                        Is.EqualTo("[Log] retained" + Environment.NewLine));
                },
                () => Assert.That(Log.Print(), Is.Empty),
            });
        }

        private static void ExecutePrefix(int stepCount, Action[] steps)
        {
            Assert.That(stepCount, Is.InRange(1, steps.Length));

            for (int index = 0; index < stepCount; index++)
                steps[index]();
        }
    }
}
