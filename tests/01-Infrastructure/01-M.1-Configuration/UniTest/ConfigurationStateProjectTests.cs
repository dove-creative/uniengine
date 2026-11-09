using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class ConfigurationStateProjectTests
    {
        // Content
        [Test]
        public Task FailedInitializationCanTransitionToTheInitializedState() =>
            InfrastructureStateProject.RunAsync(
                "Configuration.State",
                "RejectInvalidId",
                "RemainUninitialized",
                "Initialize",
                "RejectReinitialization");
    }
}
