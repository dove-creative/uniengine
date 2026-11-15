using System;
using BlackThunder.UniEngine.Core;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.Samples
{
    /// <summary>
    /// Prepares the engine once per process with a console logger and exception handler for the runnable samples.
    /// </summary>
    public static class SampleEnvironment
    {
        // Content
        public static void Initialize() =>
            Bootstrapper.Initialize(logger: WriteLog, exceptionHandler: WriteReportedException);

        private static void WriteLog(string message, LogLevel level) => Console.WriteLine($"  [{level}] {message}");

        private static void WriteReportedException(Exception exception) =>
            Console.WriteLine($"  [Reported] {exception.GetType().Name}: {exception.Message}");

        public static void Shutdown() => EventStream.Main.Dispose();
    }
}
