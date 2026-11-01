using System.Collections.Generic;

namespace Blackthunder.UniEngine.Core
{
    public static class FlagExtensions
    {
        public static bool HasAny(this int value, int any) => (value & any) != 0;
        public static bool HasAll(this int value, int all) => (value & all) == all;
        public static int Combine(this int value, int other) => value | other;
        public static int Combine(params int[] values) => Combine((IEnumerable<int>)values);
        public static int Combine(IEnumerable<int> values)
        {
            var result = 0;

            foreach (var value in values)
                result |= value;

            return result;
        }

        public static bool HasAny(this uint value, uint any) => (value & any) != 0;
        public static bool HasAll(this uint value, uint all) => (value & all) == all;
        public static uint Combine(this uint value, uint other) => value | other;
        public static uint Combine(params uint[] values) => Combine((IEnumerable<uint>)values);
        public static uint Combine(IEnumerable<uint> values)
        {
            var result = 0u;

            foreach (var value in values)
                result |= value;

            return result;
        }
    }
}
