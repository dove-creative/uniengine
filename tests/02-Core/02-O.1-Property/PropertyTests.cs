using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class PropertyTests
    {
        // Internal
        private class Payload : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose() => IsDisposed = true;
        }

        private class Subject : IPropertySubject
        {
            public Property Property { get; } = new();
        }


        // Content --------------------
        // Construction and registration
        [Test]
        public void EmptyAndNullStoresHaveNoValue()
        {
            var property = new Property();
            Assert.That(property.Count, Is.Zero);
            Assert.That(property.HasValue, Is.False);
            Assert.That((bool)property, Is.False);
            Assert.That((bool)(Property)null, Is.False);
            Assert.That(property, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConstructorStoresTheInitialValue(bool nullValue)
        {
            var value = nullValue ? null : new object();
            var property = new Property("payload", value);
            Assert.That(property.Count, Is.EqualTo(1));
            Assert.That(property.HasValue, Is.True);
            Assert.That((bool)property, Is.True);
            Assert.That(property["payload"], Is.SameAs(value));
        }

        [Test]
        public void AddReturnsThePayloadAndSupportsSelfKeys()
        {
            var property = new Property();
            var payload = new Payload();
            Assert.That(property.Add("payload", payload), Is.SameAs(payload));
            Assert.That(property.Add(payload), Is.SameAs(payload));
            Assert.That(property.Get<Payload>(payload), Is.SameAs(payload));
            Assert.That(property.Count, Is.EqualTo(2));
        }

        [TestCase("Direct")]
        [TestCase("Dictionary")]
        [TestCase("Collection")]
        public void DuplicateAddsPreserveTheOriginalValue(string route)
        {
            var original = new object();
            var property = new Property("key", original);
            TestDelegate add = route switch
            {
                "Dictionary" => () => ((IDictionary<object, object>)property).Add("key", 2),
                "Collection" => () => ((ICollection<KeyValuePair<object, object>>)property)
                    .Add(new KeyValuePair<object, object>("key", 2)),
                _ => () => property.Add("key", 2)
            };

            Assert.Throws<ArgumentException>(add);
            Assert.That(property.Get("key"), Is.SameAs(original));
            Assert.That(property.Count, Is.EqualTo(1));
        }

        [Test]
        public void KeysUseValueEqualityAndKeepDifferentTypesDistinct()
        {
            var property = new Property(new string('k', 3), 7);
            var equalKey = new string('k', 3);
            Assert.That(property.Get<int>(equalKey), Is.EqualTo(7));
            Assert.Throws<ArgumentException>(() => property.Add(equalKey, 8));
            property.Add(1, "number");
            property.Add("1", "text");
            Assert.That(property.Get<string>(1), Is.EqualTo("number"));
            Assert.That(property.Get<string>("1"), Is.EqualTo("text"));
            Assert.That(property.Remove(equalKey), Is.True);
            Assert.That(property.ContainsKey("kkk"), Is.False);
        }

        [Test]
        public void IndexerInsertsAndReplacesWithoutIncreasingTheCount()
        {
            IDictionary<object, object> property = new Property();
            property["key"] = 1;
            property["key"] = "replacement";
            Assert.That(property["key"], Is.EqualTo("replacement"));
            property["key"] = null;
            Assert.That(property["key"], Is.Null);
            Assert.That(property.Count, Is.EqualTo(1));
        }

        // Retrieval and failures
        [Test]
        public void TypedReadsAcceptAssignableTypesAndNullableValues()
        {
            var payload = new Payload();
            var property = new Property("payload", payload);
            property.Add("number", 42);
            Assert.That(property.Get<Payload>("payload"), Is.SameAs(payload));
            Assert.That(property.Get<IDisposable>("payload"), Is.SameAs(payload));
            Assert.That(property.Get<object>("payload"), Is.SameAs(payload));
            Assert.That(property.TryGet("payload", out IDisposable value), Is.True);
            Assert.That(value, Is.SameAs(payload));
            Assert.That(property.Get<int?>("number"), Is.EqualTo(42));
        }

        [Test]
        public void MissingReadsReportAbsenceWithoutChangingOtherEntries()
        {
            var property = new Property("kept", 7);
            Assert.That(property.TryGet("missing", out int number), Is.False);
            Assert.That(number, Is.Zero);
            Assert.That(property.TryGet("missing", out string text), Is.False);
            Assert.That(text, Is.Null);
            Assert.That(property.TryGet("missing", out object value), Is.False);
            Assert.That(value, Is.Null);
            Assert.That(((IDictionary<object, object>)property).TryGetValue("missing", out value), Is.False);
            Assert.That(value, Is.Null);
            Assert.Throws<KeyNotFoundException>(() => property.Get<int>("missing"));
            Assert.Throws<KeyNotFoundException>(() => property.Get("missing"));
            Assert.Throws<KeyNotFoundException>(() => _ = property["missing"]);
            Assert.That(property.Get<int>("kept"), Is.EqualTo(7));
            Assert.That(property.Count, Is.EqualTo(1));
        }

        [Test]
        public void NullValuesArePresentForReferenceAndNullableTypes()
        {
            var property = new Property("key", null);
            Assert.That(property.ContainsKey("key"), Is.True);
            Assert.That(property.Get<string>("key"), Is.Null);
            Assert.That(property.Get<int?>("key"), Is.Null);
            Assert.That(property.TryGet("key", out string text), Is.True);
            Assert.That(text, Is.Null);
            Assert.That(property.TryGet("key", out int? number), Is.True);
            Assert.That(number, Is.Null);
            Assert.That(property.TryGet("key", out object value), Is.True);
            Assert.That(value, Is.Null);
            Assert.That(((IDictionary<object, object>)property).TryGetValue("key", out value), Is.True);
            Assert.That(value, Is.Null);
        }

        [TestCase(null)]
        [TestCase("42")]
        [TestCase(42L)]
        public void TypeMismatchThrowsAndPreservesTheEntry(object value)
        {
            var property = new Property("key", value);
            Assert.Throws<InvalidCastException>(() => property.TryGet<int>("key", out _));
            Assert.Throws<InvalidCastException>(() => property.Get<int>("key"));
            Assert.Throws<InvalidCastException>(() => property.GetAndRemove<int>("key"));
            Assert.That(property.ContainsKey("key"), Is.True);
            Assert.That(property.Get("key"), Is.SameAs(value));
            Assert.That(property.Count, Is.EqualTo(1));
        }

        [TestCase("Constructor")]
        [TestCase("Add")]
        [TestCase("SelfKey")]
        [TestCase("IndexerGet")]
        [TestCase("IndexerSet")]
        [TestCase("Get")]
        [TestCase("TypedGet")]
        [TestCase("TryGet")]
        [TestCase("TypedTryGet")]
        [TestCase("ContainsKey")]
        [TestCase("Remove")]
        [TestCase("GetAndRemove")]
        [TestCase("TypedGetAndRemove")]
        public void NullKeysAreRejectedWithoutChangingTheStore(string operation)
        {
            var property = new Property("kept", 7);
            TestDelegate action = operation switch
            {
                "Constructor" => () => _ = new Property(null, 1),
                "Add" => () => property.Add(null, 1),
                "SelfKey" => () => property.Add<object>(null),
                "IndexerGet" => () => _ = property[null],
                "IndexerSet" => () => property[null] = 1,
                "Get" => () => property.Get(null),
                "TypedGet" => () => property.Get<int>(null),
                "TryGet" => () => property.TryGet(null, out object _),
                "TypedTryGet" => () => property.TryGet<int>(null, out _),
                "ContainsKey" => () => property.ContainsKey(null),
                "Remove" => () => property.Remove(null),
                "GetAndRemove" => () => property.GetAndRemove(null),
                _ => () => property.GetAndRemove<int>(null)
            };

            Assert.Throws<ArgumentNullException>(action);
            Assert.That(property.Get<int>("kept"), Is.EqualTo(7));
            Assert.That(property.Count, Is.EqualTo(1));
        }

        // Removal and ownership
        [TestCase(false)]
        [TestCase(true)]
        public void GetAndRemoveConsumesOnlyTheRequestedEntry(bool typed)
        {
            var payload = new Payload();
            var property = new Property("payload", payload);
            property.Add("kept", 7);
            var result = typed ? property.GetAndRemove<Payload>("payload") : property.GetAndRemove("payload");
            Assert.That(result, Is.SameAs(payload));
            Assert.That(property.ContainsKey("payload"), Is.False);
            Assert.That(property.Get<int>("kept"), Is.EqualTo(7));
            Assert.That(property.Count, Is.EqualTo(1));
            Assert.That(payload.IsDisposed, Is.False);
            Assert.Throws<KeyNotFoundException>(() => property.GetAndRemove<Payload>("payload"));
            Assert.Throws<KeyNotFoundException>(() => property.GetAndRemove("payload"));
            Assert.That(property.Count, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetAndRemoveConsumesNullValues(bool typed)
        {
            var property = new Property("key", null);
            var result = typed ? property.GetAndRemove<int?>("key") : property.GetAndRemove("key");
            Assert.That(result, Is.Null);
            Assert.That(property.Count, Is.Zero);
            Assert.That(property.HasValue, Is.False);
            Assert.That((bool)property, Is.False);
        }

        [TestCase("Remove")]
        [TestCase("Clear")]
        [TestCase("Replace")]
        public void ReleasingReferencesDoesNotDisposePayloads(string operation)
        {
            var payload = new Payload();
            var property = new Property("key", payload);

            if (operation == "Remove")
            {
                Assert.That(property.Remove("key"), Is.True);
                Assert.That(property.Remove("key"), Is.False);
            }
            else if (operation == "Clear")
            {
                property.Add("other", payload);
                property.Clear();
                property.Clear();
            }
            else
            {
                property["key"] = 2;
                Assert.That(property.Get<int>("key"), Is.EqualTo(2));
                property.Clear();
            }

            Assert.That(property.Count, Is.Zero);
            Assert.That((bool)property, Is.False);
            Assert.That(payload.IsDisposed, Is.False);
            property.Add("key", payload);
            Assert.That(property.Get<Payload>("key"), Is.SameAs(payload));
        }

        // Standard collection contracts
        [Test]
        public void DictionaryAndCollectionViewsShareTheSameStore()
        {
            var property = new Property();
            IDictionary<object, object> dictionary = property;
            ICollection<KeyValuePair<object, object>> collection = property;
            var keys = dictionary.Keys;
            var values = dictionary.Values;
            Assert.That(collection.IsReadOnly, Is.False);
            dictionary.Add("first", 1);
            collection.Add(new KeyValuePair<object, object>("second", null));
            Assert.That(keys, Is.EquivalentTo(new object[] { "first", "second" }));
            Assert.That(values, Is.EquivalentTo(new object[] { 1, null }));
            Assert.That(dictionary.TryGetValue("first", out var value), Is.True);
            Assert.That(value, Is.EqualTo(1));
            Assert.That(dictionary.Remove("first"), Is.True);
            Assert.That(dictionary.Remove("first"), Is.False);
            Assert.That(keys, Is.EquivalentTo(new object[] { "second" }));
            Assert.That(values, Is.EquivalentTo(new object[] { null }));
        }

        [TestCase(null)]
        [TestCase(7)]
        public void PairContainsAndRemoveRequireBothKeyAndValue(object value)
        {
            var property = new Property("key", value);
            ICollection<KeyValuePair<object, object>> collection = property;
            var matching = new KeyValuePair<object, object>("key", value);
            var wrongValue = new KeyValuePair<object, object>("key", "wrong");
            var missing = new KeyValuePair<object, object>("missing", value);
            Assert.That(collection.Contains(matching), Is.True);
            Assert.That(collection.Contains(wrongValue), Is.False);
            Assert.That(collection.Contains(missing), Is.False);
            Assert.That(collection.Remove(wrongValue), Is.False);
            Assert.That(collection.Remove(missing), Is.False);
            Assert.That(property.Get("key"), Is.SameAs(value));
            Assert.That(collection.Remove(matching), Is.True);
            Assert.That(collection.Contains(matching), Is.False);
            Assert.That(collection.Remove(matching), Is.False);
            Assert.That(property, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EnumerationAndCopyExposeEachPairWithoutRequiringOrder(bool empty)
        {
            var property = new Property();

            if (!empty)
            {
                property.Add("first", 1);
                property.Add("second", (object)null);
            }

            var expected = empty ? Array.Empty<KeyValuePair<object, object>>() : new[]
            {
                new KeyValuePair<object, object>("first", 1),
                new KeyValuePair<object, object>("second", null)
            };

            Assert.That(((IEnumerable<KeyValuePair<object, object>>)property).ToArray(), Is.EquivalentTo(expected));
            Assert.That(((IEnumerable)property).Cast<KeyValuePair<object, object>>(), Is.EquivalentTo(expected));

            var sentinel = new KeyValuePair<object, object>("untouched", new object());
            var copy = Enumerable.Repeat(sentinel, property.Count + 2).ToArray();
            ((ICollection<KeyValuePair<object, object>>)property).CopyTo(copy, 1);
            Assert.That(copy[0], Is.EqualTo(sentinel));
            Assert.That(copy[copy.Length - 1], Is.EqualTo(sentinel));
            Assert.That(copy.Skip(1).Take(property.Count), Is.EquivalentTo(expected));

            var exact = new KeyValuePair<object, object>[property.Count];
            ((ICollection<KeyValuePair<object, object>>)property).CopyTo(exact, 0);
            Assert.That(exact, Is.EquivalentTo(expected));
        }

        [Test]
        public void InvalidCopyArgumentsDoNotModifyTheTargetOrStore()
        {
            var property = new Property("key", 1);
            ICollection<KeyValuePair<object, object>> collection = property;
            var sentinel = new KeyValuePair<object, object>("untouched", 2);
            var target = new[] { sentinel };
            Assert.Throws<ArgumentNullException>(() => collection.CopyTo(null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.CopyTo(target, -1));
            Assert.Throws<ArgumentException>(() => collection.CopyTo(target, 1));
            Assert.Throws<ArgumentException>(() => collection.CopyTo(target, 2));
            Assert.Throws<ArgumentException>(() => collection.CopyTo(target, int.MaxValue));
            Assert.That(target, Is.EqualTo(new[] { sentinel }));
            Assert.That(property.Get<int>("key"), Is.EqualTo(1));
            Assert.That(property.Count, Is.EqualTo(1));

            collection.Clear();
            Assert.DoesNotThrow(() => collection.CopyTo(target, target.Length));
            Assert.Throws<ArgumentException>(() => collection.CopyTo(target, target.Length + 1));
        }

        [Test]
        public void DisplayDescribesEmptyAndNullContainingStores()
        {
            var property = new Property();
            Assert.That(property.ToString(), Is.EqualTo("Empty Property"));
            property.Add("payload", (object)null);
            var text = property.ToString();
            Assert.That(text, Does.Contain("Property [1]"));
            Assert.That(text, Does.Contain("payload: null"));
        }

        // Consumer scenario
        [Test]
        public void SubjectPayloadsRemainIndependentAcrossFailedAndSuccessfulConsumption()
        {
            IPropertySubject subject = new Subject();
            var ownerKey = new object();
            var otherKey = new object();
            var payload = new Payload();
            subject.Property.Add(ownerKey, payload);
            subject.Property.Add(otherKey, "other feature");
            Assert.Throws<InvalidCastException>(() => subject.Property.GetAndRemove<int>(ownerKey));
            Assert.That(subject.Property.Get<Payload>(ownerKey), Is.SameAs(payload));
            Assert.That(subject.Property.GetAndRemove<Payload>(ownerKey), Is.SameAs(payload));
            Assert.That(subject.Property.ContainsKey(ownerKey), Is.False);
            Assert.That(subject.Property.Get<string>(otherKey), Is.EqualTo("other feature"));
            subject.Property.Clear();
            Assert.That(subject.Property.HasValue, Is.False);
            Assert.That(payload.IsDisposed, Is.False);
            payload.Dispose();
            Assert.That(payload.IsDisposed, Is.True);
        }
    }
}
