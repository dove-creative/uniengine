using System;
using System.IO;

namespace BlackThunder.UniEngine.Infrastructure
{
    public static class Configuration
    {
        // Front
        public static string DataPath => CurrentPaths.DataPath;
        public static string InstallationPath => CurrentPaths.InstallationPath;

        // Internal
        private static readonly object _initializationLock = new();
        private static volatile Paths _paths;
        private static Paths CurrentPaths =>
            _paths ?? throw new InvalidOperationException("Configuration has not been initialized.");


        // Content
        public static void Initialize(string applicationId) => Initialize(applicationId, null);
        public static void Initialize(string applicationId, string installationPath)
        {
            lock (_initializationLock)
            {
                ThrowIfAlreadyInitialized();
                ValidateApplicationId(applicationId);

                if (installationPath != null && string.IsNullOrWhiteSpace(installationPath))
                    throw new ArgumentException("Installation path must not be empty.", nameof(installationPath));

                var resolvedInstallationPath = Path.GetFullPath(installationPath ?? AppContext.BaseDirectory);
                if (!Directory.Exists(resolvedInstallationPath))
                    throw new DirectoryNotFoundException(
                        "Installation directory does not exist: " + resolvedInstallationPath);

                var dataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BlackThunder",
                    "UniEngine",
                    "Projects",
                    applicationId);

                ValidatePath(dataPath);
                _paths = new Paths(dataPath, resolvedInstallationPath);
            }
        }

        private static void ValidateApplicationId(string applicationId)
        {
            if (string.IsNullOrWhiteSpace(applicationId))
            {
                throw new ArgumentException("Application ID must not be empty.", nameof(applicationId));
            }

            if (applicationId == "."
                || applicationId == ".."
                || applicationId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Application ID must be a valid directory name.", nameof(applicationId));
            }
        }

        private static void ValidatePath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void ThrowIfAlreadyInitialized()
        {
            if (_paths != null)
                throw new InvalidOperationException("Configuration has already been initialized.");
        }

        private class Paths
        {
            public string DataPath { get; }
            public string InstallationPath { get; }

            public Paths(string dataPath, string installationPath)
            {
                DataPath = dataPath;
                InstallationPath = installationPath;
            }
        }
    }
}
