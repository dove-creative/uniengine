using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class CollectionStateProjectTests
    {
        // Internal
        private class CollectionModel : Model
        {
            public AdaptiveCollection<int> Collection => (AdaptiveCollection<int>)Subject;
            public List<int> Expected { get; } = new();
        }

        private class CollectionProject : CompactProject<CollectionModel>
        {
            private readonly bool _deferred;
            private readonly bool _continuous;

            public CollectionProject(bool deferred, bool continuous = false) : base(ExecutionMode.Inline)
            {
                _deferred = deferred;
                _continuous = continuous;
            }

            public override IEnumerable<CompactLab<CollectionModel>> CreateCompactLabs(CollectionModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return CheckedLab("Create", state =>
                    {
                        state.Subject = new AdaptiveCollection<int>(_deferred) { 1, 2 };
                        state.Expected.AddRange(new[] { 1, 2 });
                    });
                    yield break;
                }

                if (_continuous || model.ExecutionCount == 1)
                    yield return CheckedLab("AddDuringIteration", state => AddDuringIteration(state, stopEarly: false));

                if ((_continuous || model.ExecutionCount == 2) && model.Expected.Count > 1)
                    yield return CheckedLab("RemoveDuringIteration", RemoveDuringIteration);

                if (_continuous)
                {
                    yield return CheckedLab("ClearThenAdd", ClearThenAdd);
                    yield return CheckedLab("AddThenBreak", state => AddDuringIteration(state, stopEarly: true));
                }
            }

            private static CompactLab<CollectionModel> CheckedLab(string id, Action<CollectionModel> actor) =>
                new(id, actor: actor, asserter: AssertContents);

            private void AddDuringIteration(CollectionModel model, bool stopEarly)
            {
                var collection = model.Collection;
                var added = model.ExecutionCount + 1;
                var expectedVisit = new List<int>(model.Expected);
                if (stopEarly) expectedVisit.RemoveRange(1, expectedVisit.Count - 1);
                else if (!_deferred) expectedVisit.Add(added);

                var seen = new List<int>();
                foreach (var value in collection)
                {
                    seen.Add(value);
                    if (seen.Count != 1) continue;

                    collection.Add(added);
                    model.Expected.Add(added);
                    Assert.That(collection.Count, Is.EqualTo(model.Expected.Count));
                    Assert.That(collection.Contains(added), Is.True);

                    if (stopEarly) break;
                }

                Assert.That(seen, Is.EqualTo(expectedVisit));
            }

            private static void RemoveDuringIteration(CollectionModel model)
            {
                var collection = model.Collection;
                var removed = model.Expected[1];
                model.Expected.RemoveAt(1);

                var seen = new List<int>();
                foreach (var value in collection)
                {
                    seen.Add(value);
                    if (seen.Count != 1) continue;

                    Assert.That(collection.Remove(removed), Is.True);
                    Assert.That(collection.Contains(removed), Is.False);
                }

                Assert.That(seen, Is.EqualTo(model.Expected));
            }

            private static void ClearThenAdd(CollectionModel model)
            {
                var collection = model.Collection;
                var expectedFirst = model.Expected[0];
                var added = model.ExecutionCount + 1;
                var seen = new List<int>();
                foreach (var value in collection)
                {
                    seen.Add(value);
                    if (seen.Count != 1) continue;

                    collection.Clear();
                    collection.Add(added);
                    model.Expected.Clear();
                    model.Expected.Add(added);
                }

                Assert.That(seen, Is.EqualTo(new[] { expectedFirst }));
            }

            private static void AssertContents(CollectionModel model)
            {
                var collection = model.Collection;
                Assert.That(collection.Count, Is.EqualTo(model.Expected.Count));

                var copied = new int[model.Expected.Count];
                collection.CopyTo(copied, 0);
                Assert.That(copied, Is.EqualTo(model.Expected));
                Assert.That(collection, Is.EqualTo(model.Expected));
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

        // U1: Mutations accumulate on the same logical list across successive traversal steps.
        [Category("RandomContinuous")]
        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        public Task RandomContinuousMutations(bool deferred, int seed) =>
            RandomContinuousTest.RunAsync(new CollectionProject(deferred, continuous: true), seed);
    }
}
