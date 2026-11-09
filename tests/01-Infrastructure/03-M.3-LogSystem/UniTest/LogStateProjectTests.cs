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
    }
}
