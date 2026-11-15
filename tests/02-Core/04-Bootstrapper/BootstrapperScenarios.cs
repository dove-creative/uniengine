using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using BlackThunder.UniEngine.Infrastructure;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;
using Handle = Blackthunder.UniEngine.Core.Handle;

namespace BlackThunder.UniEngine.Tests
{
    internal static class BootstrapperScenarios
    {
        private enum Phase { Update }


        // Content
        public static async Task RunAsync(string scenario, string applicationId)
        {
            var dataPath = InfrastructureProcess.GetOwnedPath(applicationId);
            var previousMain = EventStream.Main;
            Assert.That(Bootstrapper.IsInitialized, Is.False);

            switch (scenario)
            {
                case "Defaults":
                    // A test-owned entry name keeps the default application directory safe to clean up.
                    var entryBuilder = AssemblyBuilder.DefineDynamicAssembly(
                        new AssemblyName(applicationId), AssemblyBuilderAccess.Run);
                    var entryType = entryBuilder.DefineDynamicModule(applicationId).DefineType("Entry").CreateType();
                    Assembly.SetEntryAssembly(entryType.Assembly);
                    ExceptionHandler.SetHandler(_ => Assert.Fail("The old policy must be replaced."));
                    var originalOutput = Console.Out;
                    using (var output = new StringWriter())
                    {
                        try
                        {
                            Console.SetOut(output);
                            Bootstrapper.Initialize();
                            Log.Write("default console");
                            Assert.That(output.ToString(), Is.EqualTo("default console" + Environment.NewLine));
                        }
                        finally { Console.SetOut(originalOutput); }
                    }

                    var defaultFailure = new ApplicationException("default policy");
                    Assert.That(Assert.Throws<ApplicationException>(() => ExceptionHandler.Report(defaultFailure)),
                        Is.SameAs(defaultFailure));
                    Assert.That(Configuration.DataPath, Is.EqualTo(dataPath));
                    Assert.That(Configuration.InstallationPath, Is.EqualTo(Path.GetFullPath(AppContext.BaseDirectory)));
                    Assert.That(Bootstrapper.IsInitialized, Is.True);
                    Assert.That(previousMain.IsDisposed, Is.True);
                    Assert.That(EventStream.Main.Enabled, Is.True);
                    break;

                case "ReadyCallback":
                    var installationPath = Path.Combine(dataPath, "installation");
                    Directory.CreateDirectory(installationPath);
                    var marker = Path.Combine(installationPath, "resource.txt");
                    File.WriteAllText(marker, "preserve");
                    var calls = new List<string>();
                    var reported = new List<Exception>();
                    var ticks = new List<float>();
                    var oldSubscription = (Handle)previousMain.Subscribe(
                        _ => Assert.Fail("Old subscription survived."));
                    Bootstrapper.Initialize(
                        Path.Combine(installationPath, "..", "installation"),
                        (message, _) => calls.Add(message),
                        reported.Add,
                        applicationId,
                        () =>
                        {
                            Assert.That(Bootstrapper.IsInitialized, Is.False);
                            Assert.That(Configuration.InstallationPath, Is.EqualTo(installationPath));
                            Assert.That(Configuration.DataPath, Is.EqualTo(dataPath));
                            Assert.That(EventStream.Main, Is.Not.SameAs(previousMain));
                            Assert.That(EventStream.Main.Enabled, Is.True);
                            Assert.That(previousMain.IsDisposed, Is.True);
                            Log.Write("initializer");
                            EventStream.Main.Subscribe(ticks.Add);
                        });

                    Assert.That(Bootstrapper.IsInitialized, Is.True);
                    Assert.That(calls, Is.EqualTo(new[] { "initializer" }));
                    Assert.That(oldSubscription.IsDisposed, Is.True);
                    Assert.That(reported, Is.Empty);
                    EventStream.Main.OnNext(0.016f);
                    Assert.That(ticks, Is.EqualTo(new[] { 0.016f }));
                    var runtimeFailure = new ApplicationException("runtime");
                    ExceptionHandler.Report(runtimeFailure);
                    Assert.That(reported, Is.EqualTo(new[] { runtimeFailure }));
                    Assert.That(File.ReadAllText(marker), Is.EqualTo("preserve"));
                    break;

                case "InvalidInputRecovery":
                    Assert.Throws<ArgumentException>(() => Bootstrapper.Initialize(applicationId: " "));
                    Assert.Throws<DirectoryNotFoundException>(() => Bootstrapper.Initialize(
                        Path.Combine(dataPath, "missing"), applicationId: applicationId));
                    Assert.Throws<ArgumentException>(() => Bootstrapper.Initialize("", applicationId: applicationId));
                    Assert.That(Directory.Exists(dataPath), Is.False);
                    AssertUnconfigured(previousMain);
                    Bootstrapper.Initialize(applicationId: applicationId);
                    Assert.That(Bootstrapper.IsInitialized, Is.True);
                    break;

                case "MissingEntryAssembly":
                    Assembly.SetEntryAssembly(null);
                    Assert.Throws<ArgumentException>(() => Bootstrapper.Initialize());
                    AssertUnconfigured(previousMain);
                    Bootstrapper.Initialize(applicationId: applicationId);
                    Assert.That(Bootstrapper.IsInitialized, Is.True);
                    break;

                case "ConfigurationFailure":
                    var policyCalls = 0;
                    InfrastructureProcess.BlockDataPath(applicationId);
                    Assert.Throws<IOException>(() => Bootstrapper.Initialize(
                        exceptionHandler: _ => policyCalls++, applicationId: applicationId));
                    Assert.That(policyCalls, Is.Zero);
                    Assert.That(File.ReadAllText(dataPath), Is.EqualTo("test-owned path blocker"));
                    AssertUnconfigured(previousMain);
                    File.Delete(dataPath);
                    Bootstrapper.Initialize(applicationId: applicationId);
                    Assert.That(Bootstrapper.IsInitialized, Is.True);
                    break;

                case "ExistingConfiguration":
                    Configuration.Initialize(applicationId);
                    Assert.Throws<InvalidOperationException>(() =>
                        Bootstrapper.Initialize(applicationId: applicationId));
                    Assert.That(Bootstrapper.IsInitialized, Is.False);
                    Assert.That(Configuration.DataPath, Is.EqualTo(dataPath));
                    Assert.That(EventStream.Main, Is.SameAs(previousMain));
                    Assert.That(previousMain.IsDisposed, Is.False);
                    break;

                case "Duplicate":
                    Bootstrapper.Initialize(applicationId: applicationId);
                    var main = EventStream.Main;
                    Assert.Throws<InvalidOperationException>(() => Bootstrapper.Initialize(
                        applicationId: applicationId, initialize: () => Assert.Fail("Duplicate initializer ran.")));
                    Assert.That(Bootstrapper.IsInitialized, Is.True);
                    Assert.That(EventStream.Main, Is.SameAs(main));
                    Assert.That(main.IsDisposed, Is.False);
                    break;

                case "Concurrent":
                    using (var entered = new ManualResetEventSlim())
                    using (var release = new ManualResetEventSlim())
                    {
                        var initialization = Task.Run(() => Bootstrapper.Initialize(
                            applicationId: applicationId,
                            initialize: () =>
                            {
                                entered.Set();
                                Assert.That(release.Wait(TimeSpan.FromSeconds(10)), Is.True);
                            }));
                        try
                        {
                            Assert.That(entered.Wait(TimeSpan.FromSeconds(10)), Is.True);
                            Assert.That(Bootstrapper.IsInitialized, Is.False);
                            Assert.Throws<InvalidOperationException>(() =>
                                Bootstrapper.Initialize(applicationId: applicationId));
                        }
                        finally
                        {
                            release.Set();
                            await initialization;
                        }
                    }

                    Assert.That(Bootstrapper.IsInitialized, Is.True);
                    break;

                case "ReentrantCaught":
                    Bootstrapper.Initialize(applicationId: applicationId, initialize: () =>
                        Assert.Throws<InvalidOperationException>(() =>
                            Bootstrapper.Initialize(applicationId: applicationId)));
                    Assert.That(Bootstrapper.IsInitialized, Is.True);
                    break;

                case "ReentrantFailure":
                    Assert.Throws<InvalidOperationException>(() => Bootstrapper.Initialize(
                        applicationId: applicationId,
                        initialize: () => Bootstrapper.Initialize(applicationId: applicationId)));
                    Assert.That(EventStream.Main.IsDisposed, Is.True);
                    Assert.That(Bootstrapper.IsInitialized, Is.False);
                    Assert.Throws<InvalidOperationException>(() =>
                        Bootstrapper.Initialize(applicationId: applicationId));
                    break;

                case "CallbackFailure":
                case "LoggerFailure":
                case "CleanupFailure":
                    var failure = new ApplicationException("initializer failed");
                    var cleanupFailure = new InvalidOperationException("cleanup failed");
                    var reports = new List<Exception>();
                    Handle subscription = null;
                    var observed = Assert.Catch(() => Bootstrapper.Initialize(
                        logger: (_, _) => { if (scenario == "LoggerFailure") throw failure; },
                        exceptionHandler: exception =>
                        {
                            reports.Add(exception);
                            if (scenario == "CleanupFailure") throw exception;
                        },
                        applicationId: applicationId,
                        initialize: () =>
                        {
                            subscription = (Handle)EventStream.Main.Subscribe(_ => { });
                            if (scenario == "CleanupFailure") subscription.Disposed += () => throw cleanupFailure;
                            Log.Write("failure evidence");
                            throw failure;
                        }));

                    if (scenario == "CleanupFailure")
                    {
                        Assert.That(observed, Is.TypeOf<AggregateException>());
                        Assert.That(((AggregateException)observed).InnerExceptions,
                            Is.EqualTo(new Exception[] { failure, cleanupFailure }));
                        // Handle reports its own disposal callback failure; Bootstrapper never reports the initializer.
                        Assert.That(reports, Is.EqualTo(new[] { cleanupFailure }));
                    }
                    else
                    {
                        Assert.That(observed, Is.SameAs(failure));
                        Assert.That(reports, Is.Empty);
                    }

                    Assert.That(Bootstrapper.IsInitialized, Is.False);
                    Assert.That(EventStream.Main.IsDisposed, Is.True);
                    Assert.That(subscription.IsDisposed, Is.True);
                    Assert.That(Configuration.DataPath, Is.EqualTo(dataPath));
                    Assert.That(Log.Print(), Does.Contain("failure evidence"));
                    Assert.Throws<InvalidOperationException>(() =>
                        Bootstrapper.Initialize(applicationId: applicationId));
                    break;

                case "DispatchSync":
                case "DispatchParallel":
                case "DispatchCompleted":
                case "DispatchError":
                    using (var source = new EventStream<int>())
                    {
                        Exception dispatchFailure = null;
                        var attempts = 0;
                        void TryInitialize()
                        {
                            attempts++;
                            try { Bootstrapper.Initialize(applicationId: applicationId); }
                            catch (Exception exception) { dispatchFailure = exception; }
                        }

                        var path = scenario == "DispatchParallel"
                            ? source.ForOrderAsync(Phase.Update) : source.ForOrder(Phase.Update);
                        using var handle = path.Subscribe(_ => TryInitialize(), TryInitialize, _ => TryInitialize());
                        if (scenario == "DispatchCompleted") source.OnCompleted();
                        else if (scenario == "DispatchError") source.OnError(new ApplicationException("source"));
                        else source.OnNext(1);

                        Assert.That(attempts, Is.EqualTo(1));
                        Assert.That(dispatchFailure, Is.TypeOf<InvalidOperationException>());
                    }

                    AssertUnconfigured(previousMain);
                    Bootstrapper.Initialize(applicationId: applicationId);
                    Assert.That(Bootstrapper.IsInitialized, Is.True);
                    break;

                default:
                    throw new ArgumentException("Unknown Bootstrapper scenario: " + scenario);
            }
        }

        private static void AssertUnconfigured(EventStream<float> previousMain)
        {
            Assert.That(Bootstrapper.IsInitialized, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = Configuration.DataPath);
            Assert.Throws<InvalidOperationException>(() => _ = Configuration.InstallationPath);
            Assert.That(EventStream.Main, Is.SameAs(previousMain));
            Assert.That(previousMain.IsDisposed, Is.False);
        }
    }
}
