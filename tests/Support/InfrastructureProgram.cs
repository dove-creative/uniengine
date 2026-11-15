using System;
using System.Threading.Tasks;

namespace BlackThunder.UniEngine.Tests
{
    internal static class InfrastructureProgram
    {
        // Content
        private static async Task<int> Main(string[] args)
        {
            try
            {
                if (args.Length != 3 || args[0] != "--infrastructure-scenario")
                    throw new ArgumentException(
                        "Use dotnet test, or supply an infrastructure scenario and application ID.");

                InfrastructureProcess.GetOwnedPath(args[2]);

                if (args[1].StartsWith("Configuration.", StringComparison.Ordinal))
                    await ConfigurationScenarios.RunAsync(args[1].Substring("Configuration.".Length), args[2]);
                else if (args[1].StartsWith("Log.", StringComparison.Ordinal))
                    await LogScenarios.RunAsync(args[1].Substring("Log.".Length), args[2]);
                else if (args[1].StartsWith("Bootstrapper.", StringComparison.Ordinal))
                    await BootstrapperScenarios.RunAsync(args[1].Substring("Bootstrapper.".Length), args[2]);
                else
                    throw new ArgumentException("Unknown infrastructure scenario: " + args[1]);

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }
    }
}
