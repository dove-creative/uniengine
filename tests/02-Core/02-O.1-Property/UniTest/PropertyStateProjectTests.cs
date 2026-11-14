using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackThunder.UniTest;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class PropertyStateProjectTests
    {
        // Internal
        private static readonly string[] _fixedPath =
        {
            "Observe", "Missing", "NullKey", "Clear", "AddValue", "Duplicate", "Mismatch", "Replace",
            "PairMismatch", "Observe", "AddNull", "Consume", "Consume", "AddValue", "Remove",
            "AddNull", "Remove", "AddValue", "AddNull", "Clear", "Missing", "AddValue", "Observe"
        };

        private class PropertyModel : Model
        {
            public Property Property => (Property)Subject;
            public List<KeyValuePair<object, object>> Expected { get; } = new();
        }

        private class PropertyProject : CompactProject<PropertyModel>
        {
            private readonly bool _continuous;

            public PropertyProject(bool continuous = false) : base(ExecutionMode.Inline) => _continuous = continuous;

            public override IEnumerable<CompactLab<PropertyModel>> CreateCompactLabs(PropertyModel model)
            {
                if (model.ExecutionCount == 0)
                {
                    yield return CheckedLab("Create", state => state.Subject = new Property());
                    yield break;
                }

                if (!_continuous && model.ExecutionCount > _fixedPath.Length) yield break;

                foreach (var lab in CreateOperations(model))
                {
                    if (_continuous || lab.Id == _fixedPath[model.ExecutionCount - 1])
                        yield return lab;
                }
            }

            private IEnumerable<CompactLab<PropertyModel>> CreateOperations(PropertyModel model)
            {
                yield return CheckedLab("AddValue", state => Add(state, state.ExecutionCount));
                yield return CheckedLab("AddNull", state => Add(state, null));
                yield return CheckedLab("Observe", _ => { });
                yield return CheckedLab("Missing", CheckMissing);
                yield return CheckedLab("NullKey", CheckNullKey);
                yield return CheckedLab("Clear", state =>
                {
                    state.Property.Clear();
                    state.Expected.Clear();
                });

                if (model.Expected.Count == 0) yield break;

                yield return CheckedLab("Replace", Replace);
                yield return CheckedLab("Duplicate", state =>
                {
                    Assert.Throws<ArgumentException>(() => state.Property.Add(state.Expected[0].Key, "duplicate"));
                });
                yield return CheckedLab("Mismatch", state =>
                {
                    var key = state.Expected[0].Key;
                    Assert.Throws<InvalidCastException>(() => state.Property.TryGet<DateTime>(key, out _));
                    Assert.Throws<InvalidCastException>(() => state.Property.GetAndRemove<DateTime>(key));
                });
                yield return CheckedLab("PairMismatch", state =>
                {
                    ICollection<KeyValuePair<object, object>> collection = state.Property;
                    var wrong = new KeyValuePair<object, object>(state.Expected[0].Key, "wrong");
                    Assert.That(collection.Contains(wrong), Is.False);
                    Assert.That(collection.Remove(wrong), Is.False);
                });
                yield return CheckedLab("Consume", Consume);
                yield return CheckedLab("Remove", Remove);
            }

            private static CompactLab<PropertyModel> CheckedLab(string id, Action<PropertyModel> actor) =>
                new(id, actor: actor, asserter: AssertContents);

            private static void Add(PropertyModel model, object value)
            {
                var key = $"key-{model.ExecutionCount}";
                Assert.That(model.Property.Add(key, value), Is.SameAs(value));
                model.Expected.Add(new KeyValuePair<object, object>(key, value));
            }

            private static void Replace(PropertyModel model)
            {
                var key = model.Expected[0].Key;
                var value = model.ExecutionCount + 1000;
                model.Property[key] = value;
                model.Expected[0] = new KeyValuePair<object, object>(key, value);
            }

            private static void Consume(PropertyModel model)
            {
                var entry = model.Expected[0];
                Assert.That(model.Property.GetAndRemove<int?>(entry.Key), Is.EqualTo(entry.Value));
                Assert.That(model.Property.ContainsKey(entry.Key), Is.False);
                model.Expected.RemoveAt(0);
            }

            private static void Remove(PropertyModel model)
            {
                var key = model.Expected[0].Key;
                Assert.That(model.Property.Remove(key), Is.True);
                Assert.That(model.Property.Remove(key), Is.False);
                model.Expected.RemoveAt(0);
            }

            private static void CheckMissing(PropertyModel model)
            {
                Assert.That(model.Property.TryGet("missing", out int value), Is.False);
                Assert.That(value, Is.Zero);
                Assert.That(model.Property.Remove("missing"), Is.False);
                Assert.Throws<KeyNotFoundException>(() => model.Property.Get<int>("missing"));
                Assert.Throws<KeyNotFoundException>(() => model.Property.GetAndRemove<int>("missing"));
            }

            private static void CheckNullKey(PropertyModel model)
            {
                Assert.Throws<ArgumentNullException>(() => model.Property.Add(null, 1));
                Assert.Throws<ArgumentNullException>(() => model.Property[null] = 1);
                Assert.Throws<ArgumentNullException>(() => model.Property.GetAndRemove(null));
            }

            private static void AssertContents(PropertyModel model)
            {
                var property = model.Property;
                Assert.That(property.Count, Is.EqualTo(model.Expected.Count));
                Assert.That(property.HasValue, Is.EqualTo(model.Expected.Count > 0));
                Assert.That((bool)property, Is.EqualTo(model.Expected.Count > 0));
                Assert.That(property.ToArray(), Is.EquivalentTo(model.Expected));

                foreach (var entry in model.Expected)
                {
                    Assert.That(property.ContainsKey(entry.Key), Is.True);
                    Assert.That(property.Get(entry.Key), Is.EqualTo(entry.Value));
                    Assert.That(property.TryGet(entry.Key, out int? value), Is.True);
                    Assert.That(value, Is.EqualTo(entry.Value));
                }

                IDictionary<object, object> dictionary = property;
                Assert.That(dictionary.Keys, Is.EquivalentTo(model.Expected.Select(entry => entry.Key)));
                Assert.That(dictionary.Values, Is.EquivalentTo(model.Expected.Select(entry => entry.Value)));
                var copied = new KeyValuePair<object, object>[property.Count];
                ((ICollection<KeyValuePair<object, object>>)property).CopyTo(copied, 0);
                Assert.That(copied, Is.EquivalentTo(model.Expected));
            }
        }


        // Content
        [Test]
        public async Task FixedTransitionsExerciseEveryOperationIncludingFailureRecovery()
        {
            var result = await new PropertyProject().Execute(_fixedPath.Length + 1);
            Assert.That(result.AllSucceed(out var cancelled), Is.True, result.GetReport().ToJson());
            Assert.That(cancelled, Is.False);
            Assert.That(result.GetLastNode().Model.ExecutionCount, Is.EqualTo(_fixedPath.Length + 1));
        }

        [Category("RandomContinuous")]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public Task RandomContinuousMutations(int seed) =>
            RandomContinuousTest.RunAsync(new PropertyProject(continuous: true), seed);
    }
}
