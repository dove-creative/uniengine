using System;

namespace Blackthunder.UniEngine.Core
{
    public sealed class Filter<T>
    {
        // Front
        public string Name { get; }

        // Internal
        internal Func<T, bool> Predicate { get; }


        // Content
        public Filter(string name, Func<T, bool> predicate)
        {
            Name = name;
            Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }
    }
}
