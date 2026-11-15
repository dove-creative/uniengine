using System.Collections.Generic;

namespace BlackThunder.UniEngine.StateMachines.Usm.Core
{
    public interface ITransitioner
    {
        void Initialize(object owner, object blackboard, IEnumerable<INode> children);
        void Start(object input, IFrameContext context);
        OperationInfo Update(IFrameContext context);
        void Stop(DetailedOperationStatus reason, IFrameContext context);
    }
}

