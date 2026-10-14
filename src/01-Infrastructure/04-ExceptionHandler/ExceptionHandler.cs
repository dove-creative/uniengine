using System;
using System.Runtime.ExceptionServices;

namespace BlackThunder.UniEngine.Infrastructure
{
    public static class ExceptionHandler
    {
        private static readonly object _safetyLock = new();
        private static Action<Exception> _exceptionHandler;

        public static void SetHandler(Action<Exception> exceptionHandler)
        {
            lock (_safetyLock)
                _exceptionHandler = exceptionHandler;
        }

        public static void Report(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            Action<Exception> handler;
            lock (_safetyLock)
                handler = _exceptionHandler;

            if (handler != null)
                handler(exception);
            else
                ExceptionDispatchInfo.Capture(exception).Throw();
        }

        public static void SafeInvoke(this Action action)
        {
            if (action == null)
                return;

            foreach (Action handler in action.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }

        public static void SafeInvoke<T>(this Action<T> action, T param)
        {
            if (action == null)
                return;

            foreach (Action<T> handler in action.GetInvocationList())
            {
                try
                {
                    handler(param);
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }

        public static void SafeInvoke<T1, T2>(this Action<T1, T2> action, T1 param1, T2 param2)
        {
            if (action == null)
                return;

            foreach (Action<T1, T2> handler in action.GetInvocationList())
            {
                try
                {
                    handler(param1, param2);
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }

        public static void SafeInvoke<T1, T2, T3>(this Action<T1, T2, T3> action, T1 param1, T2 param2, T3 param3)
        {
            if (action == null)
                return;

            foreach (Action<T1, T2, T3> handler in action.GetInvocationList())
            {
                try
                {
                    handler(param1, param2, param3);
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }

        public static void SafeInvoke<T1, T2, T3, T4>(
            this Action<T1, T2, T3, T4> action, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            if (action == null)
                return;

            foreach (Action<T1, T2, T3, T4> handler in action.GetInvocationList())
            {
                try
                {
                    handler(param1, param2, param3, param4);
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }
    }
}
