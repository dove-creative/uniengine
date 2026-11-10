using System;
using System.Collections.Generic;
using BlackThunder.UniTest;

namespace BlackThunder.UniEngine.Tests
{
    internal class InfrastructureContinuousProject : CompactProject<Model>
    {
        // Internal
        private readonly Func<string[], IEnumerable<string>> _getCandidates;
        private readonly Action<string[]> _verifyHistory;
        private readonly object _verificationLock = new();
        private readonly HashSet<string> _verifiedHistories = new(StringComparer.Ordinal);


        // Content
        public InfrastructureContinuousProject(
            Func<string[], IEnumerable<string>> getCandidates,
            Action<string[]> verifyHistory) : base(ExecutionMode.Inline) =>
            (_getCandidates, _verifyHistory) = (getCandidates, verifyHistory);

        public override IEnumerable<CompactLab<Model>> CreateCompactLabs(Model model)
        {
            var history = model.ExecutionCount == 0 ? Array.Empty<string>() : GetSteps(model.GetExecutionHistory());

            foreach (var candidate in _getCandidates(history))
                yield return new CompactLab<Model>(candidate, actor: VerifyHistory);
        }

        private void VerifyHistory(Model model)
        {
            var history = model.GetExecutionHistory();

            // Each validation reconstructs static state from scratch. Identical ancestor histories need no recheck.
            lock (_verificationLock)
            {
                if (_verifiedHistories.Contains(history))
                    return;

                _verifyHistory(GetSteps(history));
                _verifiedHistories.Add(history);
            }
        }

        private static string[] GetSteps(string history) => history.Split(Model.Separator);
    }
}
