using System;
using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public abstract class EvaluationTransitioner : Transitioner
    {
        public LoopType LoopType { get; }
        protected INode Current { get; private set; }
        protected int CurrentIndex { get; private set; } = -1;


        protected EvaluationTransitioner(LoopType loopType = LoopType.None)
        {
            if (loopType < LoopType.None || loopType > LoopType.Forced)
                throw new ArgumentOutOfRangeException(nameof(loopType));

            LoopType = loopType;
        }

        protected void Select(int index, FrameContext frame)
        {
            var previous = Current;
            Current = null;
            CurrentIndex = -1;
            if (previous?.IsActive == true)
                previous.Stop(frame.GetOrAddNext(previous.Id, previous.LastResult.Result), DetailedOperationStatus.Preempted);

            if (!IsRunning) return;
            CurrentIndex = index;
            Current = Children[index];
        }

        protected void TryPreempt(FrameContext frame)
        {
            for (var i = 0; i < CurrentIndex && IsRunning; i++)
            {
                var child = Children[i];
                if ((child.AbortPolicies & AbortPolicies.LowerPriority) == 0) continue;
                var accepted = child.CheckCondition(frame.Payload);
                if (!IsRunning) return;
                if (!accepted) continue;
                Select(i, frame);
                return;
            }
        }

        protected OperationInfo UpdateCurrent(FrameContext frame, bool checkCondition)
        {
            var result = UpdateChild(
                Current, frame, checkCondition && (Current.AbortPolicies & AbortPolicies.Self) != 0);

            if (result.IsStopped)
            {
                Current = null;
                CurrentIndex = -1;
            }

            return result;
        }

        protected OperationInfo FinishRound(bool success, object result)
        {
            Current = null;
            CurrentIndex = -1;
            var repeat = LoopType == LoopType.Forced || (LoopType == LoopType.Conditional && success);
            return new OperationInfo(
                repeat ? DetailedOperationStatus.Running
                    : success ? DetailedOperationStatus.ChildCompleted : DetailedOperationStatus.ChildFailed,
                result);
        }

        public override void Stop(DetailedOperationStatus reason, IFrameContext context)
        {
            base.Stop(reason, context);
            Current = null;
            CurrentIndex = -1;
        }
    }
}

