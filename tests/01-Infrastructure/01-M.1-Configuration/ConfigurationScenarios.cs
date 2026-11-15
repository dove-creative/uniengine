using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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

            if (scenario.StartsWith("Continuous.", StringComparison.Ordinal))
            {
                ConfigurationContinuousScenario.VerifyHistory(
                    scenario.Substring("Continuous.".Length).Split(','), applicationId);

                return Task.CompletedTask;
            }

            if (scenario.StartsWith("State.", StringComparison.Ordinal))
            {
                var stepCount = int.Parse(scenario.Substring("State.".Length));

                Action[] steps =
                {
                    () => Assert.Throws<ArgumentException>(() => Configuration.Initialize("")),
                    () => Assert.Throws<InvalidOperationException>(() => _ = Configuration.DataPath),
                    () =>
                    {
                        Configuration.Initialize(applicationId);
                        Assert.That(Configuration.DataPath, Is.EqualTo(path));
                        Assert.That(Directory.Exists(path), Is.True);
                    },
                    () =>
                    {
                        Assert.Throws<InvalidOperationException>(() => Configuration.Initialize(applicationId));
                        Assert.That(Configuration.DataPath, Is.EqualTo(path));
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
                    Assert.Throws<InvalidOperationException>(() => _ = Configuration.DataPath);
                    Assert.Throws<InvalidOperationException>(() => _ = Configuration.InstallationPath);
                    break;
                case "InvalidIds":
                    var ids = new List<string> { null, "", " ", ".", "..", "part/child" };
                    foreach (var character in Path.GetInvalidFileNameChars())
                        ids.Add("invalid" + character + "id");

                    foreach (var invalidId in ids)
                        Assert.Throws<ArgumentException>(() => Configuration.Initialize(invalidId), "ID: " + invalidId);

                    Assert.Throws<InvalidOperationException>(() => _ = Configuration.DataPath);
                    Assert.That(Directory.Exists(path), Is.False);
                    break;
                case "AlreadyExistingDirectory":
                    Directory.CreateDirectory(path);
                    File.WriteAllText(Path.Combine(path, "existing.txt"), "preserve");
                    Configuration.Initialize(applicationId);
                    Assert.That(File.ReadAllText(Path.Combine(path, "existing.txt")), Is.EqualTo("preserve"));
                    Assert.That(Configuration.DataPath, Is.EqualTo(path));
                    break;
                case "Initialize":
                    Configuration.Initialize(applicationId);
                    Assert.That(Configuration.DataPath, Is.EqualTo(path));
                    Assert.That(Configuration.InstallationPath, Is.EqualTo(Path.GetFullPath(AppContext.BaseDirectory)));
                    Assert.That(Directory.Exists(path), Is.True);
                    break;
                case "DuplicateInitialization":
                    Configuration.Initialize(applicationId);
                    Assert.Throws<InvalidOperationException>(() => Configuration.Initialize(applicationId));
                    Assert.That(Configuration.DataPath, Is.EqualTo(path));
                    break;
                case "CreationFailure":
                    InfrastructureProcess.BlockDataPath(applicationId);
                    Assert.Throws<IOException>(() => Configuration.Initialize(applicationId));
                    Assert.Throws<InvalidOperationException>(() => _ = Configuration.DataPath);
                    Assert.That(File.ReadAllText(path), Is.EqualTo("test-owned path blocker"));
                    File.Delete(path);
                    Configuration.Initialize(applicationId);
                    Assert.That(Configuration.DataPath, Is.EqualTo(path));
                    break;
                case "InstallationPath":
                    var installation = Path.Combine(path, "installation");
                    Directory.CreateDirectory(installation);
                    var marker = Path.Combine(installation, "resource.txt");
                    File.WriteAllText(marker, "preserve");
                    var relative = Path.GetRelativePath(Environment.CurrentDirectory, installation);
                    Configuration.Initialize(applicationId, relative);
                    Assert.That(Configuration.InstallationPath, Is.EqualTo(installation));
                    Assert.That(Configuration.DataPath, Is.EqualTo(path));
                    Assert.That(File.ReadAllText(marker), Is.EqualTo("preserve"));
                    Assert.Throws<InvalidOperationException>(() => Configuration.Initialize(applicationId, "."));
                    Assert.That(Configuration.InstallationPath, Is.EqualTo(installation));
                    break;
                case "InvalidInstallationPaths":
                    Assert.Throws<ArgumentException>(() => Configuration.Initialize(applicationId, ""));
                    Assert.Throws<ArgumentException>(() => Configuration.Initialize(applicationId, " "));
                    Assert.Throws<DirectoryNotFoundException>(() => Configuration.Initialize(applicationId, path));
                    Assert.That(Directory.Exists(path), Is.False);
                    InfrastructureProcess.BlockDataPath(applicationId);
                    Assert.Throws<DirectoryNotFoundException>(() => Configuration.Initialize(applicationId, path));
                    Assert.Throws<InvalidOperationException>(() => _ = Configuration.InstallationPath);
                    Assert.Throws<InvalidOperationException>(() => _ = Configuration.DataPath);
                    File.Delete(path);
                    Configuration.Initialize(applicationId, null);
                    Assert.That(Configuration.InstallationPath, Is.EqualTo(Path.GetFullPath(AppContext.BaseDirectory)));
                    break;
                case "ConcurrentReaders":
                    using (var started = new CountdownEvent(4))
                    using (var release = new ManualResetEventSlim())
                    {
                        var readers = new Task[4];
                        var expectedInstallation = Path.GetFullPath(AppContext.BaseDirectory);
                        for (int i = 0; i < readers.Length; i++)
                        {
                            readers[i] = Task.Factory.StartNew(() =>
                            {
                                Assert.Throws<InvalidOperationException>(() => _ = Configuration.DataPath);
                                Assert.Throws<InvalidOperationException>(() => _ = Configuration.InstallationPath);
                                started.Signal();
                                Assert.That(release.Wait(TimeSpan.FromSeconds(10)), Is.True);

                                var published = SpinWait.SpinUntil(() =>
                                {
                                    string observedData;
                                    try { observedData = Configuration.DataPath; }
                                    catch (InvalidOperationException) { return false; }

                                    Assert.That(observedData, Is.EqualTo(path));
                                    Assert.That(Configuration.InstallationPath, Is.EqualTo(expectedInstallation));
                                    return true;
                                }, TimeSpan.FromSeconds(10));
                                Assert.That(published, Is.True);

                                for (int read = 0; read < 100; read++)
                                {
                                    Assert.That(Configuration.DataPath, Is.EqualTo(path));
                                    Assert.That(Configuration.InstallationPath, Is.EqualTo(expectedInstallation));
                                }
                            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                        }

                        try
                        {
                            Assert.That(started.Wait(TimeSpan.FromSeconds(10)), Is.True);
                            release.Set();
                            Configuration.Initialize(applicationId);
                        }
                        finally
                        {
                            release.Set();
                            Task.WaitAll(readers);
                        }
                    }

                    break;
                default:
                    throw new ArgumentException("Unknown Configuration scenario: " + scenario);
            }

            return Task.CompletedTask;
        }
    }
}
