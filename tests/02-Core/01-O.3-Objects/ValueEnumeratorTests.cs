using System;
using Blackthunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class ValueEnumeratorTests
    {
        // Internal
        private class NumberSource : IEnumerationSource<int>
        {
            public int BeginCount;
            public int EndCount;
            public bool FailBegin;
            public bool FailMove;
            public bool FailEnd;

            private readonly int[] _values;
            private long _nextId;
            private long _activeId;
            private int _index;

            public NumberSource(params int[] values) => _values = values;

            public long BeginEnumeration()
            {
                if (FailBegin || _activeId != 0)
                    throw new InvalidOperationException("begin");
                BeginCount++;
                _index = 0;
                return _activeId = ++_nextId;
            }

            public bool TryMoveNext(long enumerationId, out int current)
            {
                if (_activeId != enumerationId || FailMove)
                    throw new InvalidOperationException("move");
                if (_index < _values.Length)
                {
                    current = _values[_index++];
                    return true;
                }
                current = default;
                return false;
            }

            public void EndEnumeration(long enumerationId)
            {
                if (_activeId != enumerationId) return;
                _activeId = 0;
                EndCount++;
                if (FailEnd) throw new InvalidOperationException("end");
            }
        }


        // Content
        // V1: Default values are empty; explicit null sources are rejected.
        [Test]
        public void DefaultAndNullSourcesHaveDefinedBehavior()
        {
            var enumerator = default(ValueEnumerator<int>);
            Assert.That(enumerator.Current, Is.Zero);
            Assert.That(enumerator.MoveNext(), Is.False);
            enumerator.Dispose();
            enumerator.Dispose();
            Assert.That(enumerator.MoveNext(), Is.False);
            Assert.Throws<NotSupportedException>(() => enumerator.Reset());
            Assert.Throws<ArgumentNullException>(() => new ValueEnumerator<int>(null));
        }

        // V1: Disposal before the first move must not acquire the source.
        [Test]
        public void DisposalBeforeStartDoesNotTouchTheSource()
        {
            var source = new NumberSource(4);
            var enumerator = new ValueEnumerator<int>(source);
            enumerator.Dispose();
            Assert.That(enumerator.MoveNext(), Is.False);
            Assert.That(source.BeginCount, Is.Zero);
            Assert.That(source.EndCount, Is.Zero);
        }

        // V2: An unrelated source participates without any AdaptiveCollection dependency.
        [Test]
        public void IndependentSourceStartsLazilyAndEndsOnce()
        {
            var source = new NumberSource(4, 9);
            var enumerator = new ValueEnumerator<int>(source);
            Assert.That(source.BeginCount, Is.Zero);
            Assert.That(enumerator.Current, Is.Zero);
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current, Is.EqualTo(4));
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current, Is.EqualTo(9));
            Assert.That(enumerator.MoveNext(), Is.False);
            Assert.That(enumerator.Current, Is.EqualTo(9));
            Assert.That(enumerator.MoveNext(), Is.False);
            enumerator.Dispose();
            Assert.That(source.BeginCount, Is.EqualTo(1));
            Assert.That(source.EndCount, Is.EqualTo(1));
        }

        // V3: Failed acquisition never calls EndEnumeration.
        [Test]
        public void BeginFailureDoesNotReleaseAnUnacquiredSource()
        {
            var source = new NumberSource(1) { FailBegin = true };
            var enumerator = new ValueEnumerator<int>(source);
            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
            source.FailBegin = false;
            enumerator.Dispose();
            Assert.That(enumerator.MoveNext(), Is.False);
            Assert.That(source.EndCount, Is.Zero);
            var next = new ValueEnumerator<int>(source);
            Assert.That(next.MoveNext(), Is.True);
            next.Dispose();
        }

        // V3: A movement failure releases the acquired lifetime.
        [Test]
        public void MoveFailureEndsTheSourceAndAllowsAnotherEnumeration()
        {
            var source = new NumberSource(1) { FailMove = true };
            var enumerator = new ValueEnumerator<int>(source);
            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
            enumerator.Dispose();
            Assert.That(source.EndCount, Is.EqualTo(1));
            Assert.That(enumerator.MoveNext(), Is.False);
            source.FailMove = false;
            var next = new ValueEnumerator<int>(source);
            Assert.That(next.MoveNext(), Is.True);
            next.Dispose();
        }

        // V3: Cleanup failure cannot cause repeated cleanup or restart the cursor.
        [TestCase(false)]
        [TestCase(true)]
        public void CleanupFailureFinishesTheEnumerator(bool failureDuringMove)
        {
            var source = new NumberSource(1) { FailEnd = true };
            var enumerator = new ValueEnumerator<int>(source);
            Assert.That(enumerator.MoveNext(), Is.True);
            if (failureDuringMove)
            {
                source.FailMove = true;
                Assert.That(Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext()).Message, Is.EqualTo("end"));
            }
            else
            {
                Assert.That(Assert.Throws<InvalidOperationException>(() => enumerator.Dispose()).Message, Is.EqualTo("end"));
            }
            enumerator.Dispose();
            Assert.That(enumerator.MoveNext(), Is.False);
            Assert.That(source.EndCount, Is.EqualTo(1));
        }

        // V2/V3: Empty-source completion runs cleanup even when cleanup fails.
        [TestCase(false)]
        [TestCase(true)]
        public void EmptySourceCompletesItsLifetime(bool failEnd)
        {
            var source = new NumberSource { FailEnd = failEnd };
            var enumerator = new ValueEnumerator<int>(source);
            if (failEnd)
                Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
            else
                Assert.That(enumerator.MoveNext(), Is.False);
            enumerator.Dispose();
            Assert.That(source.BeginCount, Is.EqualTo(1));
            Assert.That(source.EndCount, Is.EqualTo(1));
        }
    }
}
