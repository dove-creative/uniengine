using System.Collections.Generic;
using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public class DirectTransitioner : ExplicitTransitioner
    {
        private bool _hasNext;
        private string _next;


        public void SetNext(string id)
        {
            if (id != null) id = StateMachineNames.GetName(id);
            ValidateId(id);
            _next = id;
            _hasNext = true;
        }

        public override void Initialize(object owner, object blackboard, IEnumerable<INode> children)
        {
            base.Initialize(owner, blackboard, children);
            if (_hasNext) ValidateId(_next);
        }

        public override OperationInfo Update(IFrameContext context)
        {
            var frame = FrameContext.Require(context);
            if (!IsRunning) return Running(frame);

            if (_hasNext)
            {
                var next = _next;
                _hasNext = false;
                Select(next, frame);
                if (!IsRunning) return Running(frame);
                if (next == null) return new OperationInfo(DetailedOperationStatus.ChildCompleted, frame.Payload);
            }

            if (Current == null) return Running(frame);
            var result = UpdateCurrent(frame);
            // Requests made by a child apply at the next transitioner update.
            return !IsRunning || _hasNext ? Running(frame) : result;
        }

        public override void Stop(DetailedOperationStatus reason, IFrameContext context)
        {
            base.Stop(reason, context);
            _hasNext = false;
            _next = null;
        }
    }
}

