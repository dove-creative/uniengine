using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class LogTests
    {
        // Content
        [TestCase("InitializationRequired")]
        [TestCase("DefaultConsole")]
        [TestCase("MessagesAndLevels")]
        [TestCase("Collections")]
        [TestCase("Utf8AndEmptyBuffer")]
        [TestCase("UniqueFiles")]
        [TestCase("OutputFailure")]
        [TestCase("ReentrantOutput")]
        [TestCase("StackTrace")]
        [TestCase("ResetAndReinitialize")]
        [TestCase("SaveFailure")]
        public Task PublicContractIsPreservedInAFreshProcess(string scenario) =>
            InfrastructureProcess.RunAsync("Log." + scenario);
    }
}
