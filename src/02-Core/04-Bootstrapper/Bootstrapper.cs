using System;
using System.Reflection;
using System.Threading;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.Core
{
    public static class Bootstrapper
    {
        // Front
        public static bool IsInitialized => Volatile.Read(ref _state) == (int)State.Initialized;

        // Internal
        private enum State { NotInitialized, Initializing, Initialized, Failed }
        private static int _state;


        // Content
        /// <summary>
        /// Prepares the common environment, then invokes the application's synchronous initializer.
        /// The host starts its loop only after this method returns successfully.
        /// Initialization failures propagate without being reported by the bootstrapper.
        /// </summary>
        public static void Initialize(
            string installationPath = null,
            Action<string, LogLevel> logger = null,
            Action<Exception> exceptionHandler = null,
            string applicationId = null,
            Action initialize = null)
        {
            if (EventDispatchScope.Current != null)
                throw new InvalidOperationException("Cannot bootstrap during event dispatch.");

            if (Interlocked.CompareExchange(ref _state, (int)State.Initializing, (int)State.NotInitialized)
                != (int)State.NotInitialized)
            {
                throw new InvalidOperationException("Bootstrapping has already started, completed, or failed.");
            }

            var configurationReady = false;
            EventStream<float> main = null;
            try
            {
                applicationId ??= Assembly.GetEntryAssembly()?.GetName().Name;
                if (string.IsNullOrWhiteSpace(applicationId))
                    throw new ArgumentException(
                        "An application ID or entry assembly name is required.", nameof(applicationId));

                Configuration.Initialize(applicationId, installationPath);
                configurationReady = true;
                ExceptionHandler.SetHandler(exceptionHandler);
                Log.Initialize(logger);
                EventStream.ResetMain();
                main = EventStream.Main;
                initialize?.Invoke();
                Volatile.Write(ref _state, (int)State.Initialized);
            }
            catch (Exception failure)
            {
                Volatile.Write(ref _state, (int)(configurationReady ? State.Failed : State.NotInitialized));
                try
                {
                    main?.Dispose();
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(failure, cleanupFailure);
                }

                throw;
            }
        }
    }
}
