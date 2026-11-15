using System;
using System.Collections.Generic;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public class DecisionMatrix
    {
        // Internal
        private readonly object _null = new();
        private readonly When _entry = new();
        private readonly Dictionary<string, Dictionary<DetailedOperationStatus, When>> _matrix
            = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<(Func<DetailedOperationStatus, bool> selector, When when)>> _alternatives
            = new(StringComparer.Ordinal);

        private class When
        {
            public bool HasDefault;
            public string DefaultNext;
            public readonly Dictionary<object, string> Nexts = new();
        }


        // Content --------------------
        // Configuration
        internal void AddDefaultEntryTransition(string next) => SetDefault(_entry, next);
        internal void AddEntryTransition(object input, string next) => _entry.Nexts[input ?? _null] = Normalize(next);
        internal void AddDefaultTransition(string from, DetailedOperationStatus result, string next) =>
            SetDefault(GetWhen(from, result), next);
        internal void AddDefaultTransition(string from, Func<DetailedOperationStatus, bool> selector, string next) =>
            SetDefault(GetWhen(from, selector), next);
        internal void AddTransition(string from, DetailedOperationStatus result, object input, string next) =>
            GetWhen(from, result).Nexts[input ?? _null] = Normalize(next);
        internal void AddTransition(string from, Func<DetailedOperationStatus, bool> selector, object input, string next) =>
            GetWhen(from, selector).Nexts[input ?? _null] = Normalize(next);

        private void SetDefault(When when, string next)
        {
            when.DefaultNext = Normalize(next);
            when.HasDefault = true;
        }

        private string Normalize(string id) => id == null ? null : StateMachineNames.GetName(id);

        private When GetWhen(string from, DetailedOperationStatus result)
        {
            _ = new OperationInfo(result);

            if (!_matrix.TryGetValue(from, out var results))
            {
                results = new Dictionary<DetailedOperationStatus, When>();
                _matrix.Add(from, results);
            }

            if (!results.TryGetValue(result, out var when))
            {
                when = new When();
                results.Add(result, when);
            }

            return when;
        }

        private When GetWhen(string from, Func<DetailedOperationStatus, bool> selector)
        {
            if (!_alternatives.TryGetValue(from, out var alternatives))
            {
                alternatives = new List<(Func<DetailedOperationStatus, bool>, When)>();
                _alternatives.Add(from, alternatives);
            }

            foreach (var pair in alternatives)
            {
                if (pair.selector == selector) return pair.when;
            }

            var when = new When();
            alternatives.Add((selector, when));
            return when;
        }

        // Evaluation
        public bool GetNext(string fromId, OperationInfo operation, out string nextId) =>
            fromId == null
                ? TryGetEntry(operation.Result, out nextId)
                : TryGetTransition(fromId, operation.DetailedOperationStatus, operation.Result, out nextId);

        public bool TryGetEntry(out string next) => TryGetNext(_entry, null, false, out next);
        public bool TryGetEntry(object input, out string next) => TryGetNext(_entry, input, true, out next);
        public bool TryGetTransition(string fromId, DetailedOperationStatus result, out string next) =>
            TryGetTransition(fromId, result, null, false, out next);
        public bool TryGetTransition(string fromId, DetailedOperationStatus result, object input, out string next) =>
            TryGetTransition(fromId, result, input, true, out next);

        private bool TryGetTransition(
            string fromId,
            DetailedOperationStatus result,
            object input,
            bool hasInput,
            out string next)
        {
            StateMachineNames.GetName(fromId);

            if (_matrix.TryGetValue(fromId, out var results)
                && results.TryGetValue(result, out var when)
                && TryGetNext(when, input, hasInput, out next))
                return true;

            if (_alternatives.TryGetValue(fromId, out var alternatives))
            {
                foreach (var pair in alternatives)
                {
                    if (pair.selector(result) && TryGetNext(pair.when, input, hasInput, out next)) return true;
                }
            }

            next = null;
            return false;
        }

        private bool TryGetNext(When when, object input, bool hasInput, out string next)
        {
            if (hasInput && when.Nexts.TryGetValue(input ?? _null, out next)) return true;
            next = when.DefaultNext;
            return when.HasDefault;
        }

        internal void ValidateChildren(ICollection<string> ids)
        {
            ValidateWhen(_entry);

            foreach (var from in _matrix)
            {
                ValidateId(from.Key);
                foreach (var when in from.Value.Values) ValidateWhen(when);
            }

            foreach (var from in _alternatives)
            {
                ValidateId(from.Key);
                foreach (var pair in from.Value) ValidateWhen(pair.when);
            }

            void ValidateId(string id)
            {
                if (id != null && !ids.Contains(id))
                    throw new InvalidOperationException($"The decision matrix references an unknown child: {id}.");
            }

            void ValidateWhen(When when)
            {
                if (when.HasDefault) ValidateId(when.DefaultNext);
                foreach (var id in when.Nexts.Values) ValidateId(id);
            }
        }
    }
}
