using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BlackThunder.UniEngine.Infrastructure;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    internal static class LogContinuousScenario
    {
        // Internal
        private const string WriteLog = "WriteLog";
        private const string WriteWarning = "WriteWarning";
        private const string WriteBlankLine = "WriteBlankLine";
        private const string Print = "Print";
        private const string Reset = "Reset";
        private const string Reinitialize = "Reinitialize";
        private const string BlockAndFailSave = "BlockAndFailSave";
        private const string RestoreAndSave = "RestoreAndSave";

        private static readonly string[] _regularCandidates =
        {
            WriteLog, WriteWarning, WriteBlankLine, Print, Reset, Reinitialize, BlockAndFailSave,
        };


        // Content
        public static Task RunAsync(int seed, string applicationId)
        {
            var path = InfrastructureProcess.GetOwnedPath(applicationId);
            Configuration.Initialize(applicationId);
            var project = new InfrastructureContinuousProject(GetCandidates, history =>
                VerifyHistory(history, applicationId, path));

            return RandomContinuousTest.RunAsync(project, seed, reportWriter: Console.WriteLine);
        }

        private static IEnumerable<string> GetCandidates(string[] history) =>
            history.Length > 0 && history[history.Length - 1] == BlockAndFailSave
                ? new[] { RestoreAndSave }
                : _regularCandidates;

        private static void VerifyHistory(string[] history, string applicationId, string path)
        {
            if (File.Exists(path))
                File.Delete(path);

            Directory.CreateDirectory(path);
            foreach (var file in Directory.GetFiles(path))
                File.Delete(file);

            var pending = new StringBuilder();
            var saved = new Dictionary<string, string>();
            var lastSaved = string.Empty;
            var outputGeneration = 0;
            var lastOutput = (Message: string.Empty, Level: LogLevel.Log, Generation: 0);
            InitializeLog();

            for (var stepIndex = 0; stepIndex < history.Length; stepIndex++)
            {
                var message = "기록 " + stepIndex;

                switch (history[stepIndex])
                {
                    case WriteLog:
                        Write(message, LogLevel.Log);
                        break;
                    case WriteWarning:
                        Write(message, LogLevel.Warning);
                        break;
                    case WriteBlankLine:
                        var previousOutput = lastOutput;
                        Log.WriteLine();
                        pending.AppendLine();
                        Assert.That(lastOutput, Is.EqualTo(previousOutput));
                        break;
                    case Print:
                        Save();
                        break;
                    case Reset:
                        Log.Reset();
                        pending.Clear();
                        Assert.That(Log.Print(), Is.Empty);
                        break;
                    case Reinitialize:
                        InitializeLog();
                        break;
                    case BlockAndFailSave:
                        Write(message, LogLevel.Error);

                        foreach (var file in saved.Keys)
                            File.Delete(file);

                        saved.Clear();
                        Directory.Delete(path);
                        InfrastructureProcess.BlockProjectPath(applicationId);
                        Assert.Catch<IOException>(() => Log.Print());
                        Assert.That(File.ReadAllText(path), Is.EqualTo("test-owned path blocker"));
                        break;
                    case RestoreAndSave:
                        File.Delete(path);
                        Directory.CreateDirectory(path);
                        Save();
                        break;
                    default:
                        throw new ArgumentException("Unknown Log step: " + history[stepIndex], nameof(history));
                }

                Assert.That(Log.DebugFilePath, Is.EqualTo(lastSaved));
            }

            void InitializeLog()
            {
                var generation = ++outputGeneration;
                Log.Initialize((message, level) => lastOutput = (message, level, generation));
                pending.Clear();
                lastSaved = string.Empty;
                Assert.That(Log.DebugFilePath, Is.Empty);
                Assert.That(Log.Print(), Is.Empty);
            }

            void Write(string message, LogLevel level)
            {
                lastOutput = default;
                Assert.That(Log.Write(message, level), Is.EqualTo(message));
                Assert.That(lastOutput, Is.EqualTo((message, level, outputGeneration)));
                pending.Append('[').Append(level).Append("] ").AppendLine(message);
            }

            void Save()
            {
                var expected = pending.ToString();
                Assert.That(Log.Print(), Is.EqualTo(expected));

                if (expected.Length != 0)
                {
                    lastSaved = Log.DebugFilePath;
                    Assert.That(Path.GetDirectoryName(lastSaved), Is.EqualTo(path));
                    Assert.That(saved.ContainsKey(lastSaved), Is.False);
                    saved.Add(lastSaved, expected);
                    pending.Clear();
                }

                Assert.That(Directory.GetFiles(path), Is.EquivalentTo(saved.Keys));

                foreach (var (file, contents) in saved)
                    Assert.That(File.ReadAllBytes(file), Is.EqualTo(Encoding.UTF8.GetBytes(contents)));
            }
        }
    }
}
