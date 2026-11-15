using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class ConfigurationTests
    {
        // Content
        [TestCase("Uninitialized")]
        [TestCase("InvalidIds")]
        [TestCase("Initialize")]
        [TestCase("AlreadyExistingDirectory")]
        [TestCase("DuplicateInitialization")]
        [TestCase("CreationFailure")]
        [TestCase("InstallationPath")]
        [TestCase("InvalidInstallationPaths")]
        [TestCase("ConcurrentReaders")]
        public Task PublicContractIsPreservedInAFreshProcess(string scenario) =>
            InfrastructureProcess.RunAsync("Configuration." + scenario);
    }
}
