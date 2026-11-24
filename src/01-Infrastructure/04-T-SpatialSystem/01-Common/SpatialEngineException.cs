using System;

namespace BlackThunder.UniEngine.SpatialSystem
{
    /// <summary>A framework failure identified by its adapter; it bypasses SpatialSystem error reporting.</summary>
    public sealed class SpatialEngineException : Exception
    {
        public SpatialEngineException(string message, Exception innerException)
            : base(message, innerException ?? throw new ArgumentNullException(nameof(innerException))) { }
    }
}
