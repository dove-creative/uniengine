using System;
using System.IO;

namespace BlackThunder.UniEngine.Infrastructure
{
    public static class Configuration
    {
        // Front
        public static string ProjectPath
        {
            get
            {
                if (string.IsNullOrEmpty(_projectPath))
                    throw new InvalidOperationException("Configuration has not been initialized.");

                return _projectPath;
            }
        }

        // Internal
        private static readonly object _initializationLock = new();
        private static string _projectPath;


        // Content
        public static void Initialize(string applicationId)
        {
            lock (_initializationLock)
            {
                ThrowIfAlreadyInitialized();
                ValidateApplicationId(applicationId);

                string projectPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BlackThunder",
                    "UniEngine",
                    "Projects",
                    applicationId);

                ValidatePath(projectPath);
                _projectPath = projectPath;
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
            if (!string.IsNullOrEmpty(_projectPath))
                throw new InvalidOperationException("Configuration has already been initialized.");
        }
    }
}
