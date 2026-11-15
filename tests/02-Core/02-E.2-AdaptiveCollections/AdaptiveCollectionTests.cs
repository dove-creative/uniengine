using System;
using System.Collections;
using System.Collections.Generic;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class AdaptiveCollectionTests
    {
        // Content
        // C1: Empty and populated collections preserve order, duplicates, and nulls.
        [TestCase(false)]
        [TestCase(true)]
        public void ValuesAndEmptyCollectionsRemainRepeatable(bool deferred)
        {
            var collection = new AdaptiveCollection<string>(deferred);
            Assert.That(Read(collection), Is.Empty);
            collection.AddRange(new[] { null, "same", "same", "last" });
            Assert.That(Read(collection), Is.EqualTo(new[] { null, "same", "same", "last" }));
            Assert.That(Read(collection), Is.EqualTo(new[] { null, "same", "same", "last" }));
        }

        // C1/C6: Getting a cursor does not acquire or snapshot the collection.
        [Test]
        public void EnumerationStartsOnlyOnFirstMove()
        {
            var collection = Create(true, 1);
            var unused = collection.GetEnumerator();
            var enumerator = collection.GetEnumerator();
            unused.Dispose();
            collection.Add(2);
            Assert.That(unused.MoveNext(), Is.False);
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current, Is.EqualTo(1));
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current, Is.EqualTo(2));
            Assert.That(enumerator.MoveNext(), Is.False);
            Assert.That(enumerator.Current, Is.EqualTo(2));
        }

        // C2: Pending values are visible to inspection but follow the chosen iteration mode.
        [TestCase(false)]
        [TestCase(true)]
        public void AdditionsAndInspectionRespectTheUpdateMode(bool deferred)
        {
            var collection = Create(deferred, 1, 2);
            var seen = new List<int>();
            foreach (var item in collection)
            {
                seen.Add(item);

                if (item != 1) continue;

                collection.Add(3);
                collection.AddRange(new[] { 4, 5 });
                Assert.That(collection.Count, Is.EqualTo(5));
                Assert.That(collection.Contains(5), Is.True);
                var copied = new int[7];
                collection.CopyTo(copied, 1);
                Assert.That(copied, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 0 }));
            }

            Assert.That(seen, Is.EqualTo(deferred ? new[] { 1, 2 } : new[] { 1, 2, 3, 4, 5 }));
            Assert.That(Read(collection), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        }

        // C3: Removing a previous, current, or future value preserves the remaining traversal.
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(false, 3)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        [TestCase(true, 3)]
        public void RemovalCorrectsTheCursor(bool deferred, int removed)
        {
            var collection = Create(deferred, 1, 2, 3, 4);
            var seen = new List<int>();
            foreach (var item in collection)
            {
                seen.Add(item);

                if (item == 2) Assert.That(collection.Remove(removed), Is.True);
            }

            Assert.That(seen, Is.EqualTo(removed == 3 ? new[] { 1, 2, 4 } : new[] { 1, 2, 3, 4 }));
            Assert.That(collection.Contains(removed), Is.False);
            Assert.That(collection.Count, Is.EqualTo(3));
            Assert.That(collection.Remove(99), Is.False);
        }

        // C3: Equal values remain separate entries after removing the first occurrence.
        [TestCase(false)]
        [TestCase(true)]
        public void RemovingOneDuplicateDoesNotSkipTheNext(bool deferred)
        {
            var collection = Create(deferred, 1, 1, 2);
            var seen = new List<int>();
            foreach (var item in collection)
            {
                seen.Add(item);

                if (seen.Count == 1) collection.Remove(1);
            }

            Assert.That(seen, Is.EqualTo(new[] { 1, 1, 2 }));
            Assert.That(Read(collection), Is.EqualTo(new[] { 1, 2 }));
        }

        // C2/C3: Removing a deferred value also removes it from inspection and final storage.
        [Test]
        public void PendingAdditionsCanBeRemoved()
        {
            var collection = Create(true, 1);
            foreach (var item in collection)
            {
                collection.Add(2);
                Assert.That(collection.Remove(2), Is.True);
                Assert.That(collection.Remove(2), Is.False);
                Assert.That(collection.Count, Is.EqualTo(1));
                Assert.That(collection.Contains(2), Is.False);
            }

            Assert.That(Read(collection), Is.EqualTo(new[] { 1 }));
        }

        // C4: Clear stops the current traversal while preserving additions made after Clear.
        [TestCase(false)]
        [TestCase(true)]
        public void ClearThenAddWaitsForTheNextEnumeration(bool deferred)
        {
            var collection = Create(deferred, 1, 2);
            var seen = new List<int>();
            foreach (var item in collection)
            {
                seen.Add(item);
                collection.Add(3);
                collection.Clear();
                collection.Add(4);
                Assert.That(collection.Count, Is.EqualTo(1));
            }

            Assert.That(seen, Is.EqualTo(new[] { 1 }));
            Assert.That(Read(collection), Is.EqualTo(new[] { 4 }));
        }

        // C5: Early exits commit pending additions and release the iteration guard.
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void EarlyExitRestoresTheCollection(bool deferred, bool throwInBody)
        {
            var collection = Create(deferred, 1, 2);
            try
            {
                foreach (var item in collection)
                {
                    collection.Add(3);

                    if (throwInBody) throw new ApplicationException("body");

                    break;
                }
            }
            catch (ApplicationException)
            {
                Assert.That(throwInBody, Is.True);
            }

            Assert.That(Read(collection), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        // C6: A rejected nested cursor must not release the active cursor's lifetime.
        [TestCase(false)]
        [TestCase(true)]
        public void NestedFailureLeavesTheOuterEnumerationActive(bool deferred)
        {
            var collection = Create(deferred, 1, 2);
            var outer = collection.GetEnumerator();
            Assert.That(outer.MoveNext(), Is.True);
            collection.Add(3);
            var nested = collection.GetEnumerator();
            Assert.Throws<InvalidOperationException>(() => nested.MoveNext());
            nested.Dispose();
            Assert.That(nested.MoveNext(), Is.False);
            Assert.That(outer.MoveNext(), Is.True);
            Assert.That(outer.Current, Is.EqualTo(2));
            Assert.That(outer.MoveNext(), Is.EqualTo(!deferred));
            outer.Dispose();
            Assert.That(Read(collection), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        // C7: Copies share one position and lifetime; a finished copy cannot be restarted.
        [Test]
        public void StartedCopiesShareOneEnumeration()
        {
            var collection = Create(true, 1, 2, 3);
            var first = collection.GetEnumerator();
            Assert.That(first.MoveNext(), Is.True);
            var copy = first;
            Assert.That(copy.MoveNext(), Is.True);
            Assert.That(copy.Current, Is.EqualTo(2));
            Assert.That(first.Current, Is.EqualTo(1));
            Assert.That(first.MoveNext(), Is.True);
            Assert.That(first.Current, Is.EqualTo(3));
            copy.Dispose();
            Assert.Throws<InvalidOperationException>(() => first.MoveNext());
            first.Dispose();
            Assert.That(Read(collection), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        // C7: Old copied identifiers cannot end or advance a subsequent enumeration.
        [Test]
        public void StaleCopiesCannotInterfereWithANewEnumeration()
        {
            var collection = Create(true, 1, 2);
            var first = collection.GetEnumerator();
            first.MoveNext();
            var staleDispose = first;
            var staleMove = first;
            first.Dispose();
            var current = collection.GetEnumerator();
            current.MoveNext();
            collection.Add(3);
            staleDispose.Dispose();
            Assert.Throws<InvalidOperationException>(() => staleMove.MoveNext());
            Assert.That(current.MoveNext(), Is.True);
            Assert.That(current.Current, Is.EqualTo(2));
            Assert.That(current.MoveNext(), Is.False);
            current.Dispose();
            Assert.That(Read(collection), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        // C7: Copies obtained before starting acquire separate lifetimes when used sequentially.
        [Test]
        public void UnstartedCopiesCanBeUsedSequentially()
        {
            var collection = Create(true, 1);
            var first = collection.GetEnumerator();
            var second = first;
            first.MoveNext();
            first.Dispose();
            Assert.That(second.MoveNext(), Is.True);
            second.Dispose();
        }

        // C7: The public source contract rejects stale moves and tolerates stale cleanup.
        [Test]
        public void SourceContractValidatesEnumerationIdentifiers()
        {
            var collection = Create(true, 1);
            var source = (IEnumerationSource<int>)collection;
            var id = source.BeginEnumeration();
            Assert.Throws<InvalidOperationException>(() => source.BeginEnumeration());
            source.EndEnumeration(id + 1);
            Assert.That(source.TryMoveNext(id, out var item), Is.True);
            Assert.That(item, Is.EqualTo(1));
            source.EndEnumeration(id);
            source.EndEnumeration(id);
            Assert.Throws<InvalidOperationException>(() => source.TryMoveNext(id, out _));
        }

        // C8: Both standard enumeration interfaces retain their values and disposal behavior.
        [Test]
        public void InterfaceEnumerationAndResetRemainCompatible()
        {
            var collection = Create(true, 1, 2);
            using (var generic = ((IEnumerable<int>)collection).GetEnumerator())
            {
                Assert.That(generic.Current, Is.Zero);
                Assert.That(generic.MoveNext(), Is.True);
                Assert.That(generic.Current, Is.EqualTo(1));
                Assert.Throws<NotSupportedException>(() => generic.Reset());
                collection.Add(3);
            }

            var untyped = ((IEnumerable)collection).GetEnumerator();
            var seen = new List<object>();
            try
            {
                while (untyped.MoveNext()) seen.Add(untyped.Current);

                Assert.Throws<NotSupportedException>(() => untyped.Reset());
            }
            finally
            {
                ((IDisposable)untyped).Dispose();
            }

            Assert.That(seen, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        // C8: ForEach uses the same mutation and exception cleanup rules.
        [Test]
        public void ForEachRestoresStateWhenTheSelectorThrows()
        {
            var collection = Create(true, 1, 2);

            Assert.Throws<ApplicationException>(() => collection.ForEach(item =>
            {
                collection.Add(3);
                throw new ApplicationException("selector");
            }));

            Assert.That(Read(collection), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        private static AdaptiveCollection<int> Create(bool deferred, params int[] values)
        {
            var collection = new AdaptiveCollection<int>(deferred);
            collection.AddRange(values);
            return collection;
        }

        private static List<T> Read<T>(AdaptiveCollection<T> collection)
        {
            var result = new List<T>();
            foreach (var item in collection) result.Add(item);

            return result;
        }
    }
}
