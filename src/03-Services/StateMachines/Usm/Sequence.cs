using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public class Sequence : EvaluationTransitioner
    {
        private int _nextIndex;


        public Sequence(LoopType loopType = LoopType.None) : base(loopType) { }

        public override void Start(object input, IFrameContext context)
        {
            base.Start(input, context);
            _nextIndex = 0;
        }

        public override OperationInfo Update(IFrameContext context)
        {
            var frame = FrameContext.Require(context);
            if (!IsRunning) return Running(frame);
            var checkCondition = Current != null;

            if (Current != null)
            {
                var previous = Current;
                TryPreempt(frame);
                if (!IsRunning) return Running(frame);
                _nextIndex = CurrentIndex;
                checkCondition = Current == previous;
            }

            while (_nextIndex < Children.Count)
            {
                if (Current == null)
                {
                    var accepted = Children[_nextIndex].CheckCondition(frame.Payload);
                    if (!IsRunning) return Running(frame);
                    if (!accepted)
                    {
                        _nextIndex = 0;
                        return FinishRound(false, frame.Payload);
                    }

                    Select(_nextIndex, frame);
                    if (!IsRunning) return Running(frame);
                    checkCondition = false;
                }

                var result = UpdateCurrent(frame, checkCondition);
                if (!IsRunning) return Running(frame);
                if (result.IsRunning) return result;
                if (!result.IsSuccess)
                {
                    _nextIndex = 0;
                    return FinishRound(false, result.Result);
                }

                _nextIndex++;
            }

            _nextIndex = 0;
            return FinishRound(true, frame.Payload);
        }
    }
}

