using System;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.Samples
{
    public static class ConfigurationSnippet
    {
        // Content
        public static void Initialize()
        {
            // Bootstrapper.Initialize calls this for the host. Call it directly only when not using Bootstrapper.
            // Initialize(applicationId, installationPath) uses an existing directory instead of the default.
            Configuration.Initialize("MyGame");

            var installationPath = Configuration.InstallationPath; // AppContext.BaseDirectory as a full path
            var dataPath = Configuration.DataPath; // <Documents>\BlackThunder\UniEngine\Projects\MyGame

            try
            {
                Configuration.Initialize("AnotherGame");
            }
            catch (InvalidOperationException)
            {
                // Configuration can be initialized only once per process.
            }
        }
    }
}
