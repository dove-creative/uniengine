using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    internal class InfrastructureStateProject : CompactProject<Model>
    {
        private readonly string _scenario;
        private readonly string[] _stepNames;


        // Content
        private InfrastructureStateProject(string scenario, string[] stepNames) : base(ExecutionMode.Inline) =>
            (_scenario, _stepNames) = (scenario, stepNames);

        public override IEnumerable<CompactLab<Model>> CreateCompactLabs(Model model)
        {
            if (model.ExecutionCount >= _stepNames.Length)
                return Array.Empty<CompactLab<Model>>();

            var stepCount = model.ExecutionCount + 1;

            return new[] { new CompactLab<Model>(_stepNames[stepCount - 1])
            {
                // UniTest replays prior Labs. Rebuild each real static-state prefix in a fresh process.
                Actor = state => InfrastructureProcess.RunAsync(_scenario + "." + stepCount).GetAwaiter().GetResult(),
            } };
        }

        public static async Task RunAsync(string scenario, params string[] stepNames)
        {
            var result = await new InfrastructureStateProject(scenario, stepNames).Execute(stepNames.Length);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(stepNames.Length));
        }
    }
}
