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

        // C1–C5, U1: Verify the one-time transition and path invariants through seeded histories.
        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public Task RandomContinuousStateTransitions(int seed) => ConfigurationContinuousScenario.RunAsync(seed);
    }
}
