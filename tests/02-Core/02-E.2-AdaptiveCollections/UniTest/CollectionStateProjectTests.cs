using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class CollectionStateProjectTests
    {
        // Internal
        private class CollectionProject : CompactProject<Model>
        {
            private readonly bool _deferred;

            public CollectionProject(bool deferred) : base(ExecutionMode.Inline) => _deferred = deferred;

            public override IEnumerable<CompactLab<Model>> CreateCompactLabs(Model model)
            {
                switch (model.ExecutionCount)
                {
                    case 0:
                        return new[]
                        {
                            new CompactLab<Model>("Create")
                            {
                                Actor = state => state.Subject = new AdaptiveCollection<int>(_deferred) { 1, 2 },
                                Asserter = state => Assert.That(
                                    ((AdaptiveCollection<int>)state.Subject).Count,
                                    Is.EqualTo(2)),
                            },
                        };
                    case 1:
                        return new[] { new CompactLab<Model>("AddDuringIteration") { Actor = AddDuringIteration } };
                    case 2:
                        return new[]
                        {
                            new CompactLab<Model>("RemoveDuringIteration") { Actor = RemoveDuringIteration }
                        };
                    default:
                        return Array.Empty<CompactLab<Model>>();
                }
            }

            private void AddDuringIteration(Model model)
            {
                var collection = (AdaptiveCollection<int>)model.Subject;
                var seen = new List<int>();
                foreach (var value in collection)
                {
                    seen.Add(value);

                    if (value == 1) collection.Add(3);
                }

                Assert.That(seen, Is.EqualTo(_deferred ? new[] { 1, 2 } : new[] { 1, 2, 3 }));
                Assert.That(collection.Count, Is.EqualTo(3));
            }

            private void RemoveDuringIteration(Model model)
            {
                var collection = (AdaptiveCollection<int>)model.Subject;
                var seen = new List<int>();
                foreach (var value in collection)
                {
                    seen.Add(value);

                    if (value == 1) collection.Remove(2);
                }

                Assert.That(seen, Is.EqualTo(new[] { 1, 3 }));
                Assert.That(collection.Count, Is.EqualTo(2));
            }
        }


        // Content
        // U1: UniTest replays the creation, mutation, and cleanup state transitions.
        [TestCase(false)]
        [TestCase(true)]
        public async Task StateTransitionsSucceedThroughUniTest(bool deferred)
        {
            var project = new CollectionProject(deferred);
            var result = await project.Execute(3);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(3));
        }
    }
}
