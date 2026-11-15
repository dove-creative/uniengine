using System;

namespace BlackThunder.UniEngine.StateMachines.Usm
{
    public class DecisionMatrixBuilder
    {
        private DecisionMatrix _matrix;

        public DecisionMatrixBuilder()
        {
            _matrix = new DecisionMatrix();
        }

        public FromBuilder From(string fromId)
        {
            ThrowIfAlreadyBuilt();
            return new FromBuilder(this, StateMachineNames.GetName(fromId));
        }
        public FromEntryBuilder FromEntry()
        {
            ThrowIfAlreadyBuilt();
            return new FromEntryBuilder(this);
        }

        private void AddDefaultEntryTransition(string nextId)
        {
            ThrowIfAlreadyBuilt();
            _matrix.AddDefaultEntryTransition(nextId);
        }
        private void AddEntryTransition(object input, string nextId)
        {
            ThrowIfAlreadyBuilt();
            _matrix.AddEntryTransition(input, nextId);
        }

        private void AddDefaultTransition(string fromId, DetailedOperationStatus result, string nextId)
        {
            ThrowIfAlreadyBuilt();
            _matrix.AddDefaultTransition(fromId, result, nextId);
        }
        private void AddDefaultTransition(string fromId, Func<DetailedOperationStatus, bool> selector, string nextId)
        {
            ThrowIfAlreadyBuilt();
            _matrix.AddDefaultTransition(fromId, selector, nextId);
        }
        private void AddTransition(string fromId, DetailedOperationStatus result, object input, string nextId)
        {
            ThrowIfAlreadyBuilt();
            _matrix.AddTransition(fromId, result, input, nextId);
        }
        private void AddTransition(string fromId, Func<DetailedOperationStatus, bool> selector, object input, string nextId)
        {
            ThrowIfAlreadyBuilt();
            _matrix.AddTransition(fromId, selector, input, nextId);
        }

        public DecisionMatrix Build()
        {
            ThrowIfAlreadyBuilt();

            var matrix = _matrix;
            _matrix = null;

            return matrix;
        }

        private void ThrowIfAlreadyBuilt()
        {
            if (_matrix == null)
                throw new InvalidOperationException(
                    $"{nameof(DecisionMatrixBuilder)} can no longer be used after {nameof(Build)} has been called.");
        }


        // Builders
        public class FromBuilder
        {
            internal DecisionMatrixBuilder DecisionMatrixBuilder { get; }
            private readonly string _fromId;

            internal FromBuilder(DecisionMatrixBuilder decisionMatrixBuilder, string fromId)
            {
                DecisionMatrixBuilder = decisionMatrixBuilder;
                _fromId = fromId;
            }

            internal void AddDefaultTransition(DetailedOperationStatus result, string nextId) =>
                DecisionMatrixBuilder.AddDefaultTransition(_fromId, result, nextId);
            internal void AddDefaultTransition(Func<DetailedOperationStatus, bool> selector, string nextId) =>
                DecisionMatrixBuilder.AddDefaultTransition(_fromId, selector, nextId);
            internal void AddTransition(DetailedOperationStatus result, object input, string nextId) =>
                DecisionMatrixBuilder.AddTransition(_fromId, result, input, nextId);
            internal void AddTransition(Func<DetailedOperationStatus, bool> selector, object input, string nextId) =>
                DecisionMatrixBuilder.AddTransition(_fromId, selector, input, nextId);

            public WhenBuilder When(DetailedOperationStatus result) => new WhenBuilder(this, result);
            public WhenBuilder When(Func<DetailedOperationStatus, bool> selector)
            {
                if (selector == null)
                    throw new ArgumentNullException(nameof(selector));

                return new WhenBuilder(this, selector);
            }
        }

        public class FromEntryBuilder
        {
            internal DecisionMatrixBuilder DecisionMatrixBuilder { get; }

            internal FromEntryBuilder(DecisionMatrixBuilder decisionMatrixBuilder)
            {
                DecisionMatrixBuilder = decisionMatrixBuilder;
            }

            internal void AddEntryTransition(object input, string nextId) =>
                DecisionMatrixBuilder.AddEntryTransition(input, nextId);

            public FromEntryBuilder GoTo(string nextId)
            {
                DecisionMatrixBuilder.AddDefaultEntryTransition(nextId);
                return this;
            }

            public WithEntryBuilder With(object input) => new WithEntryBuilder(this, input);

            public FromBuilder From(string fromId) => DecisionMatrixBuilder.From(fromId);
            public DecisionMatrix Build() => DecisionMatrixBuilder.Build();
        }

        public class WhenBuilder
        {
            internal FromBuilder FromBuilder { get; }
            internal DecisionMatrixBuilder DecisionMatrixBuilder => FromBuilder.DecisionMatrixBuilder;

            private readonly bool _resultIsType;
            private readonly DetailedOperationStatus _result;
            private readonly Func<DetailedOperationStatus, bool> _selector;

            internal WhenBuilder(FromBuilder fromBuilder, DetailedOperationStatus result)
            {
                FromBuilder = fromBuilder;

                _resultIsType = true;
                _result = result;
            }
            internal WhenBuilder(FromBuilder fromBuilder, Func<DetailedOperationStatus, bool> selector)
            {
                FromBuilder = fromBuilder;

                _resultIsType = false;
                _selector = selector;
            }

            internal void AddDefaultTransition(string nextId)
            {
                if (_resultIsType)
                    FromBuilder.AddDefaultTransition(_result, nextId);
                else
                    FromBuilder.AddDefaultTransition(_selector, nextId);
            }

            internal void AddTransition(object input, string nextId)
            {
                if (_resultIsType)
                    FromBuilder.AddTransition(_result, input, nextId);
                else
                    FromBuilder.AddTransition(_selector, input, nextId);
            }

            public WithBuilder With(object input) => new WithBuilder(this, input);

            public WhenBuilder GoTo(string nextId)
            {
                AddDefaultTransition(nextId);
                return this;
            }

            public WhenBuilder When(DetailedOperationStatus result) => FromBuilder.When(result);
            public WhenBuilder When(Func<DetailedOperationStatus, bool> selector)
            {
                if (selector == null)
                    throw new ArgumentNullException(nameof(selector));

                return FromBuilder.When(selector);
            }

            public FromBuilder From(string fromId) => DecisionMatrixBuilder.From(fromId);
            public FromEntryBuilder FromEntry() => DecisionMatrixBuilder.FromEntry();
            public DecisionMatrix Build() => DecisionMatrixBuilder.Build();
        }

        public class WithBuilder
        {
            internal WhenBuilder WhenBuilder { get; }
            private readonly object _input;

            internal WithBuilder(WhenBuilder whenBuilder, object input)
            {
                WhenBuilder = whenBuilder;
                _input = input;
            }

            public WhenBuilder GoTo(string nextId)
            {
                WhenBuilder.AddTransition(_input, nextId);
                return WhenBuilder;
            }
        }

        public class WithEntryBuilder
        {
            internal FromEntryBuilder FromEntryBuilder { get; }
            private readonly object _input;

            internal WithEntryBuilder(FromEntryBuilder fromEntryBuilder, object input)
            {
                FromEntryBuilder = fromEntryBuilder;
                _input = input;
            }

            public FromEntryBuilder GoTo(string nextId)
            {
                FromEntryBuilder.AddEntryTransition(_input, nextId);
                return FromEntryBuilder;
            }
        }
    }
}
