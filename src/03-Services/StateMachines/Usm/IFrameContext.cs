using System.Collections.Generic;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public interface IFrameContext
    {
        string Id { get; }
        object Payload { get; }
        IReadOnlyList<ExecutionReport> Reports { get; }
        IReadOnlyList<IFrameContext> Nexts { get; }
    }
}
