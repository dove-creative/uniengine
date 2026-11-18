using System;
using BlackThunder.UniEngine.StateMachines.Usm;
using BlackThunder.UniEngine.StateMachines.Usm.Core;

namespace BlackThunder.GeoSystem.RouteSystem
{
    internal class TracerStateMachine
    {
        private readonly Node _root;
        private bool _onSegment;
        private bool _onRoute;
        private TracerState _state;

        private class StateWork : Work
        {
            private readonly Func<bool> _condition;
            private readonly TracerState _state;

            public StateWork(TracerState state, Func<bool> condition)
            {
                _state = state;
                _condition = condition;
            }

            public override bool CheckCondition(object input) => _condition();
            public override OperationInfo Update(object input) => new(DetailedOperationStatus.Success, _state);
        }


        public TracerStateMachine()
        {
            var onTrack = new Node("OnTrack", transitioner: new Selector(), children: new[]
            {
                new Node("Rolling", new StateWork(TracerState.Rolling, () => !_onRoute)),
                new Node("Tracking", new StateWork(TracerState.Tracking, () => _onRoute))
            });
            _root = new Node("Segment", transitioner: new Selector(), children: new[]
            {
                new Node("Derailed", new StateWork(TracerState.Derailed, () => !_onSegment)),
                onTrack
            });
            _root.Initialize();
        }

        public TracerState Select(bool onSegment, bool onRoute)
        {
            if (_onSegment == onSegment && _onRoute == onRoute) return _state;
            _onSegment = onSegment;
            _onRoute = onRoute;
            _state = (TracerState)_root.Update(new FrameContext()).Result;
            return _state;
        }
    }
}
