using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class DictionaryStateProjectTests
    {
        // Internal
        private static readonly string[] _fixedPath =
        {
            "Observe", "Add", "Duplicate", "AddBatches", "Replace", "RemoveFuture", "CancelPending",
            "Break", "Throw", "ClearThenAdd", "Replace", "Remove", "Observe", "Add", "AddBatches"
        };

        private class DictionaryModel : Model
        {
            public AdaptiveDictionary<int, int> Dictionary => (AdaptiveDictionary<int, int>)Subject;
            public Dictionary<int, int> Expected { get; } = new();
            public int NextKey { get; set; } = 1;
        }

        private class DictionaryProject : CompactProject<DictionaryModel>
        {
            private readonly bool _deferred;
            private readonly bool _continuous;

            public DictionaryProject(bool deferred, bool continuous = false) : base(ExecutionMode.Inline)
            {
                _deferred = deferred;
                _continuous = continuous;
            }

            public override IEnumerable<CompactLab<DictionaryModel>> CreateCompactLabs(DictionaryModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return CheckedLab("Create", state =>
                    {
                        state.Subject = new AdaptiveDictionary<int, int>(_deferred);
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

            private IEnumerable<CompactLab<DictionaryModel>> CreateOperations(DictionaryModel model)
            {
                yield return CheckedLab("Observe", _ => { });
                yield return CheckedLab("Add", state => Add(state));
                if (model.Expected.Count == 0) yield break;

                yield return CheckedLab("Duplicate", state =>
                {
                    var key = state.Expected.Keys.Min();
                    Assert.Throws<ArgumentException>(() => state.Dictionary.Add(key, -1));
                });
                yield return CheckedLab("Remove", state =>
                {
                    var key = state.Expected.Keys.Min();
                    Assert.That(state.Dictionary.Remove(key), Is.True);
                    state.Expected.Remove(key);
                    Assert.That(state.Dictionary.Remove(key), Is.False);
                });
                yield return CheckedLab("AddBatches", AddBatches);
                yield return CheckedLab("Replace", Replace);
                yield return CheckedLab("CancelPending", CancelPending);
                yield return CheckedLab("ClearThenAdd", ClearThenAdd);
                yield return CheckedLab("Break", state => ExitEarly(state, false));
                yield return CheckedLab("Throw", state => ExitEarly(state, true));

                if (model.Expected.Count > 1) yield return CheckedLab("RemoveFuture", RemoveFuture);
            }

            private static CompactLab<DictionaryModel> CheckedLab(string id, Action<DictionaryModel> actor) =>
                new(id, actor: actor, asserter: AssertContents);

            private static KeyValuePair<int, int> Add(DictionaryModel model)
            {
                var key = model.NextKey++;
                var entry = new KeyValuePair<int, int>(key, key * 10);
                model.Dictionary.Add(entry.Key, entry.Value);
                model.Expected.Add(entry.Key, entry.Value);
                return entry;
            }

            private void AddBatches(DictionaryModel model)
            {
                var expectedVisit = model.Expected.ToList();
                var seen = new List<KeyValuePair<int, int>>();
                var firstAdded = -1;
                foreach (var entry in model.Dictionary)
                {
                    seen.Add(entry);
                    Assert.That(seen.Count, Is.LessThanOrEqualTo(expectedVisit.Count + 2));

                    if (seen.Count != 1 && entry.Key != firstAdded) continue;

                    var added = Add(model);
                    if (seen.Count == 1) firstAdded = added.Key;
                    if (!_deferred) expectedVisit.Add(added);
                    AssertLogicalContents(model);
                }

                Assert.That(seen, Is.EquivalentTo(expectedVisit));
            }

            private void Replace(DictionaryModel model)
            {
                var expectedVisit = model.Expected.ToList();
                var seen = new List<KeyValuePair<int, int>>();
                foreach (var entry in model.Dictionary)
                {
                    seen.Add(entry);
                    Assert.That(seen.Count, Is.LessThanOrEqualTo(expectedVisit.Count + 1));
                    if (seen.Count != 1) continue;

                    var replacement = new KeyValuePair<int, int>(entry.Key, -model.ExecutionCount);
                    model.Dictionary[entry.Key] = replacement.Value;
                    model.Expected[entry.Key] = replacement.Value;
                    if (!_deferred) expectedVisit.Add(replacement);
                    Assert.Throws<ArgumentException>(() => model.Dictionary.Add(entry.Key, 0));
                    AssertLogicalContents(model);
                }

                Assert.That(seen, Is.EquivalentTo(expectedVisit));
            }

            private static void RemoveFuture(DictionaryModel model)
            {
                var expectedVisit = model.Expected.ToList();
                var seen = new List<KeyValuePair<int, int>>();
                foreach (var entry in model.Dictionary)
                {
                    seen.Add(entry);
                    if (seen.Count != 1) continue;

                    var removed = expectedVisit.First(candidate => candidate.Key != entry.Key);
                    Assert.That(model.Dictionary.Remove(removed.Key), Is.True);
                    Assert.That(model.Dictionary.Remove(removed.Key), Is.False);
                    model.Expected.Remove(removed.Key);
                    expectedVisit.Remove(removed);
                    AssertLogicalContents(model);
                }

                Assert.That(seen, Is.EquivalentTo(expectedVisit));
            }

            private static void CancelPending(DictionaryModel model)
            {
                var expectedVisit = model.Expected.ToList();
                var seen = new List<KeyValuePair<int, int>>();
                foreach (var entry in model.Dictionary)
                {
                    seen.Add(entry);
                    if (seen.Count != 1) continue;

                    var added = Add(model);
                    Assert.That(model.Dictionary.Remove(added.Key), Is.True);
                    model.Expected.Remove(added.Key);
                    model.Dictionary[entry.Key] = -1;
                    Assert.That(model.Dictionary.Remove(entry.Key), Is.True);
                    model.Expected.Remove(entry.Key);
                    AssertLogicalContents(model);
                }

                Assert.That(seen, Is.EquivalentTo(expectedVisit));
            }

            private static void ClearThenAdd(DictionaryModel model)
            {
                var visits = 0;
                foreach (var entry in model.Dictionary)
                {
                    visits++;
                    Assert.That(visits, Is.EqualTo(1));
                    Add(model);
                    model.Dictionary.Clear();
                    model.Expected.Clear();
                    Add(model);
                    AssertLogicalContents(model);
                }

                Assert.That(visits, Is.EqualTo(1));
            }

            private static void ExitEarly(DictionaryModel model, bool fail)
            {
                var failure = new ApplicationException("iteration body");
                var visits = 0;
                void Traverse()
                {
                    foreach (var entry in model.Dictionary)
                    {
                        visits++;
                        model.Dictionary.Remove(entry.Key);
                        model.Expected.Remove(entry.Key);
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

            private static void AssertLogicalContents(DictionaryModel model)
            {
                Assert.That(model.Dictionary.Count, Is.EqualTo(model.Expected.Count));
                for (var key = 0; key <= model.NextKey; key++)
                {
                    var exists = model.Expected.TryGetValue(key, out var expected);
                    Assert.That(model.Dictionary.ContainsKey(key), Is.EqualTo(exists));
                    Assert.That(model.Dictionary.TryGetValue(key, out var actual), Is.EqualTo(exists));
                    Assert.That(actual, Is.EqualTo(expected));
                    if (exists) Assert.That(model.Dictionary[key], Is.EqualTo(expected));
                }
            }

            private static void AssertContents(DictionaryModel model)
            {
                AssertLogicalContents(model);
                Assert.That(model.Dictionary.ToArray(), Is.EquivalentTo(model.Expected));
                var copied = new KeyValuePair<int, int>[model.Expected.Count];
                model.Dictionary.CopyTo(copied, 0);
                Assert.That(copied, Is.EquivalentTo(model.Expected));
                Assert.That(model.Dictionary.Keys, Is.EquivalentTo(model.Expected.Keys));
                Assert.That(model.Dictionary.Values, Is.EquivalentTo(model.Expected.Values));
            }
        }


        // Content
        // U2: Every operation is exercised by a fixed path in both update modes.
        [TestCase(false)]
        [TestCase(true)]
        public async Task FixedMutations(bool deferred)
        {
            var result = await new DictionaryProject(deferred).Execute(_fixedPath.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_fixedPath.Length + 1));
        }

        // U2: The same operations accumulate on an independent dictionary model.
        [Category("RandomContinuous")]
        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        public Task RandomContinuousMutations(bool deferred, int seed) =>
            RandomContinuousTest.RunAsync(new DictionaryProject(deferred, continuous: true), seed);
    }
}
