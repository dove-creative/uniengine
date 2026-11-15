using System;

namespace BlackThunder.UniEngine.Core
{
    public static class EventStream
    {
        /// <summary>
        /// Gets the default tick stream, carrying elapsed seconds since the previous tick.
        /// The host owns publication and termination on its event thread.
        /// </summary>
        public static EventStream<float> Main { get; private set; } = new();

        /// <summary>
        /// Disposes the previous main stream without completion notification and creates an enabled, empty stream.
        /// Call on the host event thread outside event dispatch, then register the new session's subscriptions.
        /// </summary>
        /// <exception cref="InvalidOperationException">An event dispatch is in progress on this thread.</exception>
        public static void ResetMain()
        {
            if (EventDispatchScope.Current != null)
                throw new InvalidOperationException("Cannot reset the main stream during event dispatch.");

            Main.Dispose();
            Main = new EventStream<float>();
        }
    }
}
