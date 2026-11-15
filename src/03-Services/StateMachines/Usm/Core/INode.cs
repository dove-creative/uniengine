using System.Collections.Generic;

namespace BlackThunder.UniEngine.StateMachines.Usm.Core
{
    public interface INode
    {
        string Id { get; }
        IReadOnlyList<INode> Children { get; }
        IWork Work { get; }
        ITransitioner Transitioner { get; }
        AbortPolicies AbortPolicies { get; }
        ReframeOption ReframeOption { get; set; }
        bool IsActive { get; }
        OperationInfo LastResult { get; }

        void Initialize(object owner = null, object blackboard = null);
        bool CheckCondition(object input);
        OperationInfo Update(IFrameContext context);
        void Stop(IFrameContext context, DetailedOperationStatus reason = DetailedOperationStatus.HaltedByExternal);
    }
}

