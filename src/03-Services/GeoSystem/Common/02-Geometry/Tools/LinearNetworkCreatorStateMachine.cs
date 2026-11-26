using System;
using BlackThunder.UniEngine.StateMachines.Usm;
using BlackThunder.UniEngine.StateMachines.Usm.Core;
using StateNode = BlackThunder.UniEngine.StateMachines.Usm.Node;

namespace BlackThunder.GeoSystem.Geometry
{
    internal class LinearNetworkCreatorStateMachine
    {
        private readonly StateNode _root;
        private bool _creating;
        private bool _hasAnchor;
        private bool _hasCandidate;
        private LinearNetworkCreatorState _state;

        private class ConditionWork : Work
        {
            private readonly Func<bool> _condition;

            public ConditionWork(Func<bool> condition) => _condition = condition;
            public override bool CheckCondition(object input) => _condition();
        }

        private class StateWork : ConditionWork
        {
            private readonly LinearNetworkCreatorState _state;

            public StateWork(LinearNetworkCreatorState state, Func<bool> condition) : base(condition) => _state = state;
            public override OperationInfo Update(object input) => new(DetailedOperationStatus.Success, _state);
        }


        public LinearNetworkCreatorStateMachine()
        {
            var anchor = new StateNode("Anchor", new ConditionWork(() => !_hasAnchor), new Selector(), new[]
            {
                Leaf(LinearNetworkCreatorState.PutFirstNode, () => !_hasCandidate),
                Leaf(LinearNetworkCreatorState.SelectFirstNode, () => _hasCandidate),
            });

            var connect = new StateNode("Connect", new ConditionWork(() => _hasAnchor), new Selector(), new[]
            {
                Leaf(LinearNetworkCreatorState.PutNextNode, () => !_hasCandidate),
                Leaf(LinearNetworkCreatorState.SelectNextNode, () => _hasCandidate),
            });

            var create = new StateNode("Create", new ConditionWork(() => _creating), new Selector(),
                new[] { anchor, connect });

            _root = new StateNode("NetworkCreator", transitioner: new Selector(), children: new[]
            {
                Leaf(LinearNetworkCreatorState.Idle, () => !_creating),
                create,
            });

            _root.Initialize();
        }

        private StateNode Leaf(LinearNetworkCreatorState state, Func<bool> condition) =>
            new(state.ToString(), new StateWork(state, condition));

        public LinearNetworkCreatorState Select(bool creating, bool hasAnchor, bool hasCandidate)
        {
            if (_creating == creating && _hasAnchor == hasAnchor && _hasCandidate == hasCandidate) return _state;
            _creating = creating;
            _hasAnchor = hasAnchor;
            _hasCandidate = hasCandidate;
            _state = (LinearNetworkCreatorState)_root.Update(new FrameContext()).Result;
            return _state;
        }
    }
}
