using System.Collections.Generic;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Samples
{
    public static class CoreObjectsSnippet
    {
        // Content
        public static void Enumerate()
        {
            var countdown = new Countdown(3);
            var values = new List<int>();

            // foreach uses the struct enumerator directly, so the enumeration itself does not box.
            // The first MoveNext begins the enumeration, and reaching the end ends it.
            foreach (var value in countdown)
                values.Add(value);
            // values = 3, 2, 1

            // Leaving the loop early still ends the enumeration, because foreach disposes the enumerator.
            foreach (var value in countdown)
            {
                if (value == 3) break;
            }
        }

        /// <summary>
        /// Counts down from a start value; each enumeration keeps its own cursor.
        /// </summary>
        private class Countdown : IEnumerationSource<int>
        {
            // Internal
            private readonly int _start;
            private readonly Dictionary<long, int> _cursors = new();
            private long _lastEnumerationId;


            // Content
            public Countdown(int start) => _start = start;

            public ValueEnumerator<int> GetEnumerator() => new(this);

            long IEnumerationSource<int>.BeginEnumeration()
            {
                var enumerationId = ++_lastEnumerationId;
                _cursors.Add(enumerationId, _start);
                return enumerationId;
            }

            bool IEnumerationSource<int>.TryMoveNext(long enumerationId, out int current)
            {
                if (!_cursors.TryGetValue(enumerationId, out current) || current <= 0)
                {
                    current = default;
                    return false;
                }

                _cursors[enumerationId] = current - 1;
                return true;
            }

            void IEnumerationSource<int>.EndEnumeration(long enumerationId) => _cursors.Remove(enumerationId);
        }
    }
}
