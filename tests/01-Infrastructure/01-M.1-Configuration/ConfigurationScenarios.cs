using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BlackThunder.UniEngine.Infrastructure;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    internal static class ConfigurationScenarios
    {
        // Content
        public static Task RunAsync(string scenario, string applicationId)
        {
            var path = InfrastructureProcess.GetOwnedPath(applicationId);

            if (scenario.StartsWith("State.", StringComparison.Ordinal))
            {
                var stepCount = int.Parse(scenario.Substring("State.".Length));

                Action[] steps =
                {
                    () => Assert.Throws<ArgumentException>(() => Configuration.Initialize("")),
                    () => Assert.Throws<InvalidOperationException>(() => _ = Configuration.ProjectPath),
                    () =>
                    {
                        Configuration.Initialize(applicationId);
                        Assert.That(Configuration.ProjectPath, Is.EqualTo(path));
                        Assert.That(Directory.Exists(path), Is.True);
                    },
                    () =>
                    {
                        Assert.Throws<InvalidOperationException>(() => Configuration.Initialize(applicationId));
                        Assert.That(Configuration.ProjectPath, Is.EqualTo(path));
                    },
                };

                Assert.That(stepCount, Is.InRange(1, steps.Length));

                for (int index = 0; index < stepCount; index++)
                    steps[index]();

                return Task.CompletedTask;
            }

            switch (scenario)
            {
                case "Uninitialized":
                    Assert.Throws<InvalidOperationException>(() => _ = Configuration.ProjectPath);
                    break;
                case "InvalidIds":
                    var ids = new List<string> { null, "", " ", ".", "..", "part/child" };
                    foreach (var character in Path.GetInvalidFileNameChars())
                        ids.Add("invalid" + character + "id");

                    foreach (var invalidId in ids)
                        Assert.Throws<ArgumentException>(() => Configuration.Initialize(invalidId), "ID: " + invalidId);

                    Assert.Throws<InvalidOperationException>(() => _ = Configuration.ProjectPath);
                    Assert.That(Directory.Exists(path), Is.False);
                    break;
                case "AlreadyExistingDirectory":
                    Directory.CreateDirectory(path);
                    File.WriteAllText(Path.Combine(path, "existing.txt"), "preserve");
                    Configuration.Initialize(applicationId);
                    Assert.That(File.ReadAllText(Path.Combine(path, "existing.txt")), Is.EqualTo("preserve"));
                    Assert.That(Configuration.ProjectPath, Is.EqualTo(path));
                    break;
                case "Initialize":
                    Configuration.Initialize(applicationId);
                    Assert.That(Configuration.ProjectPath, Is.EqualTo(path));
                    Assert.That(Directory.Exists(path), Is.True);
                    break;
                case "DuplicateInitialization":
                    Configuration.Initialize(applicationId);
                    Assert.Throws<InvalidOperationException>(() => Configuration.Initialize(applicationId));
                    Assert.That(Configuration.ProjectPath, Is.EqualTo(path));
                    break;
                case "CreationFailure":
                    InfrastructureProcess.BlockProjectPath(applicationId);
                    Assert.Throws<IOException>(() => Configuration.Initialize(applicationId));
                    Assert.Throws<InvalidOperationException>(() => _ = Configuration.ProjectPath);
                    Assert.That(File.ReadAllText(path), Is.EqualTo("test-owned path blocker"));
                    File.Delete(path);
                    Configuration.Initialize(applicationId);
                    Assert.That(Configuration.ProjectPath, Is.EqualTo(path));
                    break;
                default:
                    throw new ArgumentException("Unknown Configuration scenario: " + scenario);
            }

            return Task.CompletedTask;
        }
    }
}
