using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class HashSetStateProjectTests
    {
        // Internal
        private static readonly string[] _fixedPath =
        {
            "Observe", "Add", "Duplicate", "AddBatches", "RemoveFuture", "Readd", "CancelPending",
            "Break", "Throw", "ClearThenAdd", "Readd", "Remove", "Observe", "Add", "AddBatches"
        };

        private class SetModel : Model
        {
            public AdaptiveHashSet<int> Set => (AdaptiveHashSet<int>)Subject;
            public HashSet<int> Expected { get; } = new();
            public int NextValue { get; set; } = 1;
        }

        private class SetProject : CompactProject<SetModel>
        {
            private readonly bool _deferred;
            private readonly bool _continuous;

            public SetProject(bool deferred, bool continuous = false) : base(ExecutionMode.Inline)
            {
                _deferred = deferred;
                _continuous = continuous;
            }

            public override IEnumerable<CompactLab<SetModel>> CreateCompactLabs(SetModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return CheckedLab("Create", state =>
                    {
                        state.Subject = new AdaptiveHashSet<int>(_deferred);
                        for (var index = 0; index < 3; index++) Add(state);
                    });
                    yield break;
                }

                if (!_continuous && model.ExecutionCount > _fixedPath.Length) yield break;

                foreach (var lab in CreateOperations(model))
                {
                    if (_continuous || lab.Id == _fixedPath[model.ExecutionCount - 1]) yield return lab;
                }
            }

            private IEnumerable<CompactLab<SetModel>> CreateOperations(SetModel model)
            {
                yield return CheckedLab("Observe", _ => { });
                yield return CheckedLab("Add", state => Add(state));
                if (model.Expected.Count == 0) yield break;

                yield return CheckedLab("Duplicate", state => state.Set.Add(state.Expected.Min()));
                yield return CheckedLab("Remove", state =>
                {
                    var value = state.Expected.Min();
                    Assert.That(state.Set.Remove(value), Is.True);
                    state.Expected.Remove(value);
                    Assert.That(state.Set.Remove(value), Is.False);
                });
                yield return CheckedLab("AddBatches", AddBatches);
                yield return CheckedLab("Readd", Readd);
                yield return CheckedLab("CancelPending", CancelPending);
                yield return CheckedLab("ClearThenAdd", ClearThenAdd);
                yield return CheckedLab("Break", state => ExitEarly(state, false));
                yield return CheckedLab("Throw", state => ExitEarly(state, true));

                if (model.Expected.Count > 1) yield return CheckedLab("RemoveFuture", RemoveFuture);
            }

            private static CompactLab<SetModel> CheckedLab(string id, Action<SetModel> actor) =>
                new(id, actor: actor, asserter: AssertContents);

            private static int Add(SetModel model)
            {
                var value = model.NextValue++;
                model.Set.Add(value);
                model.Expected.Add(value);
                return value;
            }

            private void AddBatches(SetModel model)
            {
                var expectedVisit = model.Expected.ToList();
                var seen = new List<int>();
                var firstAdded = -1;
                foreach (var value in model.Set)
                {
                    seen.Add(value);
                    Assert.That(seen.Count, Is.LessThanOrEqualTo(expectedVisit.Count + 2));
                    if (seen.Count != 1 && value != firstAdded) continue;

                    var added = Add(model);
                    model.Set.Add(added);
                    if (seen.Count == 1) firstAdded = added;
                    if (!_deferred) expectedVisit.Add(added);
                    AssertLogicalContents(model);
                }

                Assert.That(seen, Is.EquivalentTo(expectedVisit));
            }

            private static void RemoveFuture(SetModel model)
            {
                var expectedVisit = model.Expected.ToList();
                var seen = new List<int>();
                foreach (var value in model.Set)
                {
                    seen.Add(value);
                    if (seen.Count != 1) continue;

                    var removed = expectedVisit.First(candidate => candidate != value);
                    Assert.That(model.Set.Remove(removed), Is.True);
                    Assert.That(model.Set.Remove(removed), Is.False);
                    model.Expected.Remove(removed);
                    expectedVisit.Remove(removed);
                    AssertLogicalContents(model);
                }

                Assert.That(seen, Is.EquivalentTo(expectedVisit));
            }

            private void Readd(SetModel model)
            {
                var expectedVisit = model.Expected.ToList();
                var seen = new List<int>();
                foreach (var value in model.Set)
                {
                    seen.Add(value);
                    Assert.That(seen.Count, Is.LessThanOrEqualTo(expectedVisit.Count + 1));
                    if (seen.Count != 1) continue;

                    Assert.That(model.Set.Remove(value), Is.True);
                    Assert.That(model.Set.Contains(value), Is.False);
                    model.Set.Add(value);
                    model.Set.Add(value);
                    if (!_deferred) expectedVisit.Add(value);
                    AssertLogicalContents(model);
                }

                Assert.That(seen, Is.EquivalentTo(expectedVisit));
            }

            private static void CancelPending(SetModel model)
            {
                var expectedVisit = model.Expected.ToList();
                var seen = new List<int>();
                foreach (var value in model.Set)
                {
                    seen.Add(value);
                    if (seen.Count != 1) continue;

                    var added = Add(model);
                    model.Set.Add(added);
                    Assert.That(model.Set.Remove(added), Is.True);
                    Assert.That(model.Set.Remove(added), Is.False);
                    model.Expected.Remove(added);
                    AssertLogicalContents(model);
                }

                Assert.That(seen, Is.EquivalentTo(expectedVisit));
            }

            private static void ClearThenAdd(SetModel model)
            {
                var visits = 0;
                foreach (var value in model.Set)
                {
                    visits++;
                    Assert.That(visits, Is.EqualTo(1));
                    Add(model);
                    model.Set.Clear();
                    model.Expected.Clear();
                    Add(model);
                    AssertLogicalContents(model);
                }

                Assert.That(visits, Is.EqualTo(1));
            }

            private static void ExitEarly(SetModel model, bool fail)
            {
                var failure = new ApplicationException("iteration body");
                var visits = 0;
                void Traverse()
                {
                    foreach (var value in model.Set)
                    {
                        visits++;
                        model.Set.Remove(value);
                        model.Expected.Remove(value);
                        Add(model);
                        AssertLogicalContents(model);
                        if (fail) throw failure;
                        break;
                    }
                }

                if (fail) Assert.That(Assert.Throws<ApplicationException>(Traverse), Is.SameAs(failure));
                else Traverse();
                Assert.That(visits, Is.EqualTo(1));
            }

            private static void AssertLogicalContents(SetModel model)
            {
                Assert.That(model.Set.Count, Is.EqualTo(model.Expected.Count));
                for (var value = 0; value <= model.NextValue; value++)
                    Assert.That(model.Set.Contains(value), Is.EqualTo(model.Expected.Contains(value)));
            }

            private static void AssertContents(SetModel model)
            {
                AssertLogicalContents(model);
                Assert.That(model.Set.ToArray(), Is.EquivalentTo(model.Expected));
                var copied = new int[model.Expected.Count];
                model.Set.CopyTo(copied, 0);
                Assert.That(copied, Is.EquivalentTo(model.Expected));
            }
        }


        // Content
        // U3: Every operation is exercised by a fixed path in both update modes.
        [TestCase(false)]
        [TestCase(true)]
        public async Task FixedMutations(bool deferred)
        {
            var result = await new SetProject(deferred).Execute(_fixedPath.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_fixedPath.Length + 1));
        }

        // U3: The same operations accumulate on an independent set model.
        [Category("RandomContinuous")]
        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        public Task RandomContinuousMutations(bool deferred, int seed) =>
            RandomContinuousTest.RunAsync(new SetProject(deferred, continuous: true), seed);
    }
}
