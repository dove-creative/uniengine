using System;
using System.Collections.Generic;

namespace BlackThunder.UniEngine.StateMachines.Usm.Core
{
    public class FrameContext : IFrameContext
    {
        // Front
        public string Id { get; internal set; }
        public object Payload { get; set; }
        public IReadOnlyList<ExecutionReport> Reports { get; }
        public IReadOnlyList<FrameContext> Nexts { get; }
        public int UpdateCount => _budget.Count;
        public int MaxNodeUpdates => _budget.Limit;
        IReadOnlyList<IFrameContext> IFrameContext.Nexts => Nexts;

        // Internal
        private readonly List<ExecutionReport> _reports = new();
        private readonly List<FrameContext> _nexts = new();
        private readonly UpdateBudget _budget;

        private class UpdateBudget
        {
            public int Count;
            public int Limit;
        }


        // Content
        public FrameContext(object payload = null, int maxNodeUpdates = 1024)
        {
            if (maxNodeUpdates < 1) throw new ArgumentOutOfRangeException(nameof(maxNodeUpdates));
            Payload = payload;
            _budget = new UpdateBudget { Limit = maxNodeUpdates };
            Reports = _reports.AsReadOnly();
            Nexts = _nexts.AsReadOnly();
        }

        private FrameContext(string id, object payload, UpdateBudget budget)
        {
            Id = id;
            Payload = payload;
            _budget = budget;
            Reports = _reports.AsReadOnly();
            Nexts = _nexts.AsReadOnly();
        }

        public FrameContext GetOrAddNext(string id, object payload)
        {
            id = StateMachineNames.GetName(id);

            if (_nexts.Count > 0 && _nexts[_nexts.Count - 1].Id == id)
            {
                var last = _nexts[_nexts.Count - 1];
                last.Payload = payload;
                return last;
            }

            var next = new FrameContext(id, payload, _budget);
            _nexts.Add(next);
            return next;
        }

        public void Report(
            ElementType element,
            FrameType frame,
            ExecutionPhase phase,
            OperationInfo result = default) => _reports.Add(new ExecutionReport(element, frame, phase, result));

        internal void ConsumeUpdate()
        {
            if (_budget.Count >= _budget.Limit)
                throw new InvalidOperationException("The frame exceeded its node update limit.");

            _budget.Count++;
        }

        internal static FrameContext Require(IFrameContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return context as FrameContext
                ?? throw new ArgumentException("A FrameContext is required for execution.", nameof(context));
        }
    }
}
