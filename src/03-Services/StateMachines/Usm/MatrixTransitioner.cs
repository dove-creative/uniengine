using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public class MatrixTransitioner : ExplicitTransitioner
    {
        public DecisionMatrix DecisionMatrix { get; }


        public MatrixTransitioner(DecisionMatrix matrix)
        {
            DecisionMatrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
        }

        public override void Initialize(object owner, object blackboard, IEnumerable<INode> children)
        {
            base.Initialize(owner, blackboard, children);
            DecisionMatrix.ValidateChildren(Children.Select(child => child.Id).ToArray());
        }

        public override OperationInfo Update(IFrameContext context)
        {
            var frame = FrameContext.Require(context);
            while (IsRunning)
            {
                if (Current == null)
                {
                    var found = DecisionMatrix.GetNext(null, Running(frame), out var entry);
                    if (!IsRunning || !found) return Running(frame);
                    if (entry == null) return new OperationInfo(DetailedOperationStatus.ChildCompleted, frame.Payload);
                    Select(entry, frame);
                }

                var current = Current;
                var result = UpdateCurrent(frame);
                if (!IsRunning) return Running(frame);
                var hasNext = DecisionMatrix.GetNext(current.Id, result, out var next);
                if (!IsRunning) return Running(frame);
                if (!hasNext) return result;
                if (next == current.Id) return Running(frame);
                Select(next, frame);
                if (!IsRunning) return Running(frame);
                if (next == null) return new OperationInfo(DetailedOperationStatus.ChildCompleted, frame.Payload);
            }

            return Running(frame);
        }
    }
}

