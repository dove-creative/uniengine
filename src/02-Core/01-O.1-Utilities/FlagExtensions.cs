using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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

        public static bool HasAny(this ulong value, ulong any) => (value & any) != 0;
        public static bool HasAll(this ulong value, ulong all) => (value & all) == all;
        public static ulong Combine(this ulong value, ulong other) => value | other;
        public static ulong Combine(params ulong[] values) => Combine((IEnumerable<ulong>)values);
        public static ulong Combine(IEnumerable<ulong> values)
        {
            var result = 0ul;

            foreach (var value in values)
                result |= value;

            return result;
        }

        public static ulong ToBits<TFlags>(this TFlags value) where TFlags : struct, Enum
        {
            var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));
            return bytes.Length switch
            {
                1 => bytes[0],
                2 => MemoryMarshal.Read<ushort>(bytes),
                4 => MemoryMarshal.Read<uint>(bytes),
                _ => MemoryMarshal.Read<ulong>(bytes)
            };
        }
    }
}
