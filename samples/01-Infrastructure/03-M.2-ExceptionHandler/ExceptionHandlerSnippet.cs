using System;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.Samples
{
    public static class ExceptionHandlerSnippet
    {
        // Content
        public static void ReportAndSafeInvoke()
        {
            // The handler is shared by the whole process. A host usually sets it through Bootstrapper.Initialize.
            ExceptionHandler.SetHandler(exception => Console.Error.WriteLine($"Reported: {exception.Message}"));

            Action<string> greeted = null;
            greeted += name => Console.WriteLine($"First handler: hello {name}");
            greeted += _ => throw new InvalidOperationException("Second handler failed.");
            greeted += name => Console.WriteLine($"Third handler: hello {name}");

            // SafeInvoke reports the failing handler and keeps calling the rest.
            greeted.SafeInvoke("UniEngine");
            // First handler: hello UniEngine
            // Reported: Second handler failed.
            // Third handler: hello UniEngine

            // Report passes an exception to the handler instead of throwing it.
            ExceptionHandler.Report(new TimeoutException("Reported without a throw."));
        }

        public static void RethrowWithoutHandler()
        {
            ExceptionHandler.SetHandler(null);

            try
            {
                ExceptionHandler.Report(new ArgumentException("No handler is set."));
            }
            catch (ArgumentException)
            {
                // Without a handler, Report rethrows the exception.
            }
        }
    }
}
