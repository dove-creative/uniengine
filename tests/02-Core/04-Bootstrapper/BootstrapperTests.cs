using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class BootstrapperTests
    {
        [TestCase("Defaults")]
        [TestCase("ReadyCallback")]
        [TestCase("InvalidInputRecovery")]
        [TestCase("MissingEntryAssembly")]
        [TestCase("ConfigurationFailure")]
        [TestCase("ExistingConfiguration")]
        [TestCase("Duplicate")]
        [TestCase("Concurrent")]
        [TestCase("ReentrantCaught")]
        [TestCase("ReentrantFailure")]
        [TestCase("CallbackFailure")]
        [TestCase("LoggerFailure")]
        [TestCase("CleanupFailure")]
        [TestCase("DispatchSync")]
        [TestCase("DispatchParallel")]
        [TestCase("DispatchCompleted")]
        [TestCase("DispatchError")]
        public Task PublicContractIsPreservedInAFreshProcess(string scenario) =>
            InfrastructureProcess.RunAsync("Bootstrapper." + scenario);
    }
}
