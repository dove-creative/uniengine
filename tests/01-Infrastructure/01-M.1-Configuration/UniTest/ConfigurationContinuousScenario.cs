using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BlackThunder.UniEngine.Infrastructure;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    internal static class ConfigurationContinuousScenario
    {
        // Internal
        private const int Depth = 8;
        private const string ScenarioPrefix = "Configuration.Continuous.";
        private const string ReadBeforeInitialization = "ReadBeforeInitialization";
        private const string RejectInvalidId = "RejectInvalidId";
        private const string FailOnBlockedPath = "FailOnBlockedPath";
        private const string PrepareExistingDirectory = "PrepareExistingDirectory";
        private const string RecoverPath = "RecoverPath";
        private const string Initialize = "Initialize";
        private const string ReadInitializedPath = "ReadInitializedPath";
        private const string RejectReinitialization = "RejectReinitialization";


        // Content
        public static Task RunAsync(int seed)
        {
            var project = new InfrastructureContinuousProject(GetCandidates, history =>
                InfrastructureProcess.RunAsync(ScenarioPrefix + string.Join(',', history)).GetAwaiter().GetResult());

            return RandomContinuousTest.RunAsync(project, seed, Depth);
        }

        private static IEnumerable<string> GetCandidates(string[] history) => history.Length switch
        {
            0 => new[] { ReadBeforeInitialization, RejectInvalidId },
            1 => new[] { FailOnBlockedPath, PrepareExistingDirectory },
            2 when history[1] == FailOnBlockedPath => new[] { RecoverPath },
            2 => new[] { ReadBeforeInitialization, RejectInvalidId },
            3 => new[] { Initialize },
            _ => new[] { ReadInitializedPath, RejectReinitialization },
        };

        public static void VerifyHistory(string[] history, string applicationId)
        {
            var path = InfrastructureProcess.GetOwnedPath(applicationId);
            var sentinel = Path.Combine(path, "existing.txt");
            var prepared = false;
            var initialized = false;

            foreach (var step in history)
            {
                switch (step)
                {
                    case ReadBeforeInitialization:
                    case ReadInitializedPath:
                        // The path invariant is checked after every transition, including a direct read.
                        break;
                    case RejectInvalidId:
                        Assert.Throws<ArgumentException>(() => Configuration.Initialize(".."));
                        break;
                    case FailOnBlockedPath:
                        InfrastructureProcess.BlockDataPath(applicationId);
                        Assert.Throws<IOException>(() => Configuration.Initialize(applicationId));
                        Assert.That(File.ReadAllText(path), Is.EqualTo("test-owned path blocker"));
                        break;
                    case PrepareExistingDirectory:
                        Directory.CreateDirectory(path);
                        File.WriteAllText(sentinel, "preserve");
                        prepared = true;
                        break;
                    case RecoverPath:
                        File.Delete(path);
                        break;
                    case Initialize:
                        Configuration.Initialize(applicationId);
                        initialized = true;
                        break;
                    case RejectReinitialization:
                        Assert.Throws<InvalidOperationException>(() => Configuration.Initialize(applicationId));
                        break;
                    default:
                        throw new ArgumentException("Unknown Configuration step: " + step, nameof(history));
                }

                if (initialized)
                    Assert.That(Configuration.DataPath, Is.EqualTo(path));
                else
                    Assert.Throws<InvalidOperationException>(() => _ = Configuration.DataPath);

                Assert.That(Directory.Exists(path), Is.EqualTo(initialized || prepared));

                if (prepared)
                    Assert.That(File.ReadAllText(sentinel), Is.EqualTo("preserve"));
            }
        }
    }
}
