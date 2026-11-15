using System;
using System.Runtime.ExceptionServices;
using BlackThunder.UniEngine.Infrastructure;

namespace BlackThunder.UniEngine.Core
{
    internal partial class EventGraph<T>
    {
        private class DebugNode : Node
        {
            private readonly string _message;
            private readonly LogLevel _logType;

            internal DebugNode(string message, LogLevel logType)
            {
                _message = message;
                _logType = logType;
            }

            internal override void Visit(
                T value,
                long lastVisible,
                ref ExceptionDispatchInfo failure,
                ParallelBatch batch = null)
            {
                try
                {
                    $"{value}: {_message}".Write(_logType);
                }
                catch (Exception exception)
                {
                    Fail(exception, ref failure);
                    return;
                }

                base.Visit(value, lastVisible, ref failure, batch);
            }
        }
    }
}
