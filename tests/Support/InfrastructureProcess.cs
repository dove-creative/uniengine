using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    internal static class InfrastructureProcess
    {
        private const string ApplicationPrefix = "UniEngineTests-";


        // Content
        public static async Task RunAsync(string scenario, bool writeOutput = false, int timeoutSeconds = 30)
        {
            var applicationId = ApplicationPrefix + Guid.NewGuid().ToString("N");
            var dataPath = GetOwnedPath(applicationId);
            try
            {
                var host = Path.GetFullPath(
                    Path.Combine(
                        RuntimeEnvironment.GetRuntimeDirectory(),
                        "..",
                        "..",
                        "..",
                        OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));

                var startInfo = new ProcessStartInfo(host)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                startInfo.ArgumentList.Add(typeof(InfrastructureProcess).Assembly.Location);
                startInfo.ArgumentList.Add("--infrastructure-scenario");
                startInfo.ArgumentList.Add(scenario);
                startInfo.ArgumentList.Add(applicationId);

                using var process = Process.Start(startInfo);
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                var exitTask = process.WaitForExitAsync();

                var timedOut = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) != exitTask;
                if (timedOut)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) when (process.HasExited)
                    {
                        // The child may finish between timeout detection and termination.
                    }
                }

                await exitTask;
                var output = await outputTask;
                var error = await errorTask;
                if (writeOutput)
                    TestContext.Out.Write(output);

                Assert.That(timedOut, Is.False, scenario + $" exceeded {timeoutSeconds} seconds.\n" + output + error);
                Assert.That(process.ExitCode, Is.Zero, scenario + "\n" + output + error);
            }
            finally
            {
                // Revalidate the exact test-owned target before recursive cleanup.
                Assert.That(GetOwnedPath(applicationId), Is.EqualTo(dataPath));

                if (Directory.Exists(dataPath))
                    Directory.Delete(dataPath, recursive: true);
                else if (File.Exists(dataPath))
                    File.Delete(dataPath);
            }
        }

        public static string GetOwnedPath(string applicationId)
        {
            if (!applicationId.StartsWith(ApplicationPrefix, StringComparison.Ordinal)
                || !Guid.TryParseExact(applicationId.Substring(ApplicationPrefix.Length), "N", out _))
            {
                throw new ArgumentException("A test-owned application ID is required.", nameof(applicationId));
            }

            var parent = Path.GetFullPath(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BlackThunder",
                    "UniEngine",
                    "Projects"));
            var path = Path.GetFullPath(Path.Combine(parent, applicationId));
            if (!string.Equals(Path.GetDirectoryName(path), parent, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The test path escaped its parent directory.");

            return path;
        }

        public static void BlockDataPath(string applicationId)
        {
            var path = GetOwnedPath(applicationId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "test-owned path blocker");
        }
    }
}
