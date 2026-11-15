using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public class Selector : EvaluationTransitioner
    {
        public Selector(LoopType loopType = LoopType.None) : base(loopType) { }

        public override OperationInfo Update(IFrameContext context)
        {
            var frame = FrameContext.Require(context);
            if (!IsRunning) return Running(frame);
            if (Children.Count == 0) return FinishRound(true, frame.Payload);
            var nextIndex = 0;

            if (Current != null)
            {
                var previous = Current;
                TryPreempt(frame);
                if (!IsRunning) return Running(frame);
                var child = Current;
                nextIndex = CurrentIndex + 1;
                var result = UpdateCurrent(frame, Current == previous);
                if (!IsRunning) return Running(frame);
                if (result.IsRunning) return result;
                if (result.IsSuccess) return FinishRound(true, result.Result);
                if (!ShouldAdvance(child, result)) return Running(frame);
            }

            for (var i = nextIndex; i < Children.Count; i++)
            {
                var child = Children[i];
                var accepted = child.CheckCondition(frame.Payload);
                if (!IsRunning) return Running(frame);
                if (!accepted) continue;
                Select(i, frame);
                if (!IsRunning) return Running(frame);
                var result = UpdateCurrent(frame, false);
                if (!IsRunning) return Running(frame);
                if (result.IsRunning) return result;
                if (result.IsSuccess) return FinishRound(true, result.Result);
                if (!ShouldAdvance(child, result)) return Running(frame);
            }

            return FinishRound(false, frame.Payload);
        }

        private bool ShouldAdvance(INode child, OperationInfo result) =>
            result.DetailedOperationStatus == DetailedOperationStatus.HaltedBySelf
            || (result.IsFailure && (child.AbortPolicies & AbortPolicies.StopOnFailure) != 0);
    }
}

