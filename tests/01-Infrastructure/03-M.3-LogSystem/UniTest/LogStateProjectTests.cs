using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class LogStateProjectTests
    {
        // Content
        [Test]
        public Task LifecycleSucceedsThroughUniTest() =>
            InfrastructureStateProject.RunAsync(
                "Log.Lifecycle",
                "Initialize",
                "WriteFirst",
                "SaveFirst",
                "EmptyBuffer",
                "WritePending",
                "Reinitialize",
                "WriteSecond",
                "SaveSecond");

        [Test]
        public Task FailedSaveRecoversThroughUniTest() =>
            InfrastructureStateProject.RunAsync(
                "Log.SaveRecovery",
                "SavePrevious",
                "BlockPath",
                "FailSave",
                "RestoreDirectory",
                "SaveRetainedBuffer",
                "EmptyBuffer");

        // L2, L4, L5, L7, L8, U1, U2: Accumulate writes, resets, saves, and recovery in a seeded history.
        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public Task RandomContinuousStateTransitions(int seed) =>
            InfrastructureProcess.RunAsync("Log.Continuous." + seed, writeOutput: true, timeoutSeconds: 120);
    }
}
