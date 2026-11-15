using System;
using System.Collections;
using System.Collections.Generic;

namespace BlackThunder.UniEngine.Core
{
    /// <summary>
    /// Enumerates a source with a value-type cursor and lazy lifetime acquisition.
    /// </summary>
    /// <remarks>
    /// Copies made after starting share one source enumeration, not independent cursors.
    /// Returning this struct through an enumerator interface boxes it.
    /// </remarks>
    public struct ValueEnumerator<T> : IEnumerator<T>
    {
        // Front
        public T Current => _current;
        object IEnumerator.Current => Current;

        // Internal
        private readonly IEnumerationSource<T> _source;
        private long _enumerationId;
        private T _current;
        private bool _isStarted;
        private bool _isFinished;


        // Content
        public ValueEnumerator(IEnumerationSource<T> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _enumerationId = 0;
            _current = default;
            _isStarted = false;
            _isFinished = false;
        }

        public bool MoveNext()
        {
            if (_isFinished) return false;

            if (_source == null)
            {
                _isFinished = true;
                return false;
            }

            if (!_isStarted)
            {
                // A failed acquisition must not release another enumerator's lifetime.
                _isFinished = true;
                _enumerationId = _source.BeginEnumeration();
                _isStarted = true;
                _isFinished = false;
            }

            try
            {
                if (_source.TryMoveNext(_enumerationId, out var current))
                {
                    _current = current;
                    return true;
                }
            }
            catch
            {
                Dispose();
                throw;
            }

            Dispose();
            return false;
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose()
        {
            if (_isFinished) return;
            _isFinished = true;

            if (_isStarted)
                _source.EndEnumeration(_enumerationId);
        }
    }
}
