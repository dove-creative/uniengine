using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public abstract class Transitioner : ITransitioner
    {
        protected IReadOnlyList<INode> Children { get; private set; }
        protected bool IsRunning { get; private set; }


        public virtual void Initialize(object owner, object blackboard, IEnumerable<INode> children)
        {
            if (Children != null) throw new InvalidOperationException("The transitioner is already initialized.");
            if (children == null) throw new ArgumentNullException(nameof(children));
            Children = Array.AsReadOnly(children.ToArray());
        }

        public virtual void Start(object input, IFrameContext context) => IsRunning = true;
        public abstract OperationInfo Update(IFrameContext context);
        public virtual void Stop(DetailedOperationStatus reason, IFrameContext context) => IsRunning = false;

        protected OperationInfo UpdateChild(INode child, FrameContext frame, bool checkCondition)
        {
            var childFrame = frame.GetOrAddNext(child.Id, frame.Payload);
            if (checkCondition)
            {
                var accepted = child.CheckCondition(frame.Payload);
                if (!IsRunning) return Running(frame);
                if (!accepted)
                {
                    var rejected = ((Node)child).RejectCondition(childFrame);
                    return rejected;
                }
            }

            var result = child.Update(childFrame);
            if (IsRunning) frame.Payload = result.Result;
            return result;
        }

        protected OperationInfo Running(IFrameContext context) =>
            new OperationInfo(DetailedOperationStatus.Running, context.Payload);
    }
}

