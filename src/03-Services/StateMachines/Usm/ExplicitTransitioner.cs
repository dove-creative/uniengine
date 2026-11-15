using System;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public abstract class ExplicitTransitioner : Transitioner
    {
        protected INode Current { get; private set; }
        private Dictionary<string, INode> _children;


        public override void Initialize(object owner, object blackboard, IEnumerable<INode> children)
        {
            base.Initialize(owner, blackboard, children);
            _children = Children.ToDictionary(child => child.Id, StringComparer.Ordinal);
        }

        protected void ValidateId(string id)
        {
            if (id != null && _children != null && !_children.ContainsKey(id))
                throw new InvalidOperationException($"Unknown child: {id}.");
        }

        protected void Select(string id, FrameContext frame)
        {
            if (Current?.Id == id) return;
            var previous = Current;
            Current = null;
            if (previous?.IsActive == true)
                previous.Stop(frame.GetOrAddNext(previous.Id, previous.LastResult.Result), DetailedOperationStatus.Preempted);

            if (IsRunning && id != null) Current = _children[id];
        }

        protected OperationInfo UpdateCurrent(FrameContext frame) =>
            UpdateChild(Current, frame, (Current.AbortPolicies & AbortPolicies.Self) != 0);

        public override void Stop(DetailedOperationStatus reason, IFrameContext context)
        {
            base.Stop(reason, context);
            Current = null;
        }
    }
}

