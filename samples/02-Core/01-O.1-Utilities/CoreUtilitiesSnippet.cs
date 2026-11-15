using System;
using BlackThunder.UniEngine.Core;

namespace BlackThunder.UniEngine.Samples
{
    public static class CoreUtilitiesSnippet
    {
        // Internal
        [Flags]
        private enum Elements
        {
            None = 0,
            Fire = 1,
            Ice = 2,
            Poison = 4
        }


        // Content
        public static void CombineFlags()
        {
            // ToBits turns any flags enum into ulong bits that Combine, HasAny, and HasAll accept.
            var hit = Elements.Fire.ToBits().Combine(Elements.Poison.ToBits()); // 5
            var coldOrToxic = Elements.Ice.ToBits().Combine(Elements.Poison.ToBits());
            var fireAndIce = Elements.Fire.ToBits().Combine(Elements.Ice.ToBits());
            var hasAny = hit.HasAny(coldOrToxic); // true, Poison is shared
            var hasAll = hit.HasAll(fireAndIce); // false, Ice is missing

            var mask = FlagExtensions.Combine(1, 4, 8); // 13
        }

        public static void CompareTokens()
        {
            // IsValidAndSameTokenWith matches only a stored token that exists and is the same instance.
            var current = new Token();
            Token stored = current;
            Token empty = null;
            var matchesCurrent = stored.IsValidAndSameTokenWith(current); // true
            var matchesNew = stored.IsValidAndSameTokenWith(new Token()); // false
            var emptyMatches = empty.IsValidAndSameTokenWith(null); // false, an empty slot never matches
        }
    }
}
