using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BlackThunder.UniEngine.Core;
using NUnit.Framework;

namespace BlackThunder.UniEngine.Tests
{
    public class FlagExtensionsTests
    {
        // Internal
        private enum ByteCode : byte { High = 0x80, All = byte.MaxValue }
        private enum SByteCode : sbyte { High = sbyte.MinValue, All = -1 }
        private enum ShortCode : short { High = short.MinValue, All = -1 }
        private enum UShortCode : ushort { High = 0x8000, All = ushort.MaxValue }
        private enum IntCode : int { High = 1 << 31, All = -1 }
        private enum UIntCode : uint { High = 0x80000000, All = uint.MaxValue }
        private enum LongCode : long { High = long.MinValue, All = -1 }
        private enum ULongCode : ulong { High = 1ul << 63, All = ulong.MaxValue }


        // Content --------------------
        // Inclusion and combination
        [TestCase(0ul, 1ul, false)]
        [TestCase(3ul, 0ul, false)]
        [TestCase(5ul, 3ul, true)]
        [TestCase(5ul, 10ul, false)]
        [TestCase(0x8000000000000001ul, 0x8000000000000000ul, true)]
        public void ULongHasAnyRequiresAtLeastOneSharedBit(ulong value, ulong mask, bool expected)
        {
            Assert.That(value.HasAny(mask), Is.EqualTo(expected));
        }

        [TestCase(0ul, 0ul, true)]
        [TestCase(3ul, 0ul, true)]
        [TestCase(7ul, 3ul, true)]
        [TestCase(5ul, 3ul, false)]
        [TestCase(0x8000000000000001ul, 0x8000000000000000ul, true)]
        public void ULongHasAllRequiresEveryMaskBit(ulong value, ulong mask, bool expected)
        {
            Assert.That(value.HasAll(mask), Is.EqualTo(expected));
        }

        [Test]
        public void ULongPairCombinationPreservesAllBits()
        {
            Assert.That(0x8000000000000001ul.Combine(3ul), Is.EqualTo(0x8000000000000003ul));
            Assert.That(0ul.Combine(0ul), Is.Zero);
        }

        [Test]
        public void ULongArrayCombinationIncludesEveryValue()
        {
            Assert.That(FlagExtensions.Combine(1ul, 3ul, 1ul << 63), Is.EqualTo(0x8000000000000003ul));
            Assert.That(FlagExtensions.Combine(Array.Empty<ulong>()), Is.Zero);
        }

        [Test]
        public void ULongEnumerableCombinationIncludesEveryValue()
        {
            var values = new List<ulong> { 1ul, 3ul, 1ul << 63 };
            Assert.That(FlagExtensions.Combine(values), Is.EqualTo(0x8000000000000003ul));
            Assert.That(FlagExtensions.Combine((IEnumerable<ulong>)Array.Empty<ulong>()), Is.Zero);
        }

        // Enum conversion accepts ordinary enums as well as flags enums.
        [Test]
        public void ByteBitsAreZeroExtended()
        {
            Assert.That(ByteCode.High.ToBits(), Is.EqualTo(0x80ul));
            Assert.That(ByteCode.All.ToBits(), Is.EqualTo(0xfful));
            Assert.That(default(ByteCode).ToBits(), Is.Zero);
        }

        [Test]
        public void SByteBitsAreZeroExtended()
        {
            Assert.That(SByteCode.High.ToBits(), Is.EqualTo(0x80ul));
            Assert.That(SByteCode.All.ToBits(), Is.EqualTo(0xfful));
            Assert.That(default(SByteCode).ToBits(), Is.Zero);
        }

        [Test]
        public void ShortBitsAreZeroExtended()
        {
            Assert.That(ShortCode.High.ToBits(), Is.EqualTo(0x8000ul));
            Assert.That(ShortCode.All.ToBits(), Is.EqualTo(0xfffful));
            Assert.That(default(ShortCode).ToBits(), Is.Zero);
        }

        [Test]
        public void UShortBitsAreZeroExtended()
        {
            Assert.That(UShortCode.High.ToBits(), Is.EqualTo(0x8000ul));
            Assert.That(UShortCode.All.ToBits(), Is.EqualTo(0xfffful));
            Assert.That(default(UShortCode).ToBits(), Is.Zero);
        }

        [Test]
        public void IntBitsAreZeroExtended()
        {
            Assert.That(IntCode.High.ToBits(), Is.EqualTo(0x80000000ul));
            Assert.That(IntCode.All.ToBits(), Is.EqualTo(0xfffffffful));
            Assert.That(default(IntCode).ToBits(), Is.Zero);
        }

        [Test]
        public void UIntBitsAreZeroExtended()
        {
            Assert.That(UIntCode.High.ToBits(), Is.EqualTo(0x80000000ul));
            Assert.That(UIntCode.All.ToBits(), Is.EqualTo(0xfffffffful));
            Assert.That(default(UIntCode).ToBits(), Is.Zero);
        }

        [Test]
        public void LongBitsPreserveTheFullWidth()
        {
            Assert.That(LongCode.High.ToBits(), Is.EqualTo(0x8000000000000000ul));
            Assert.That(LongCode.All.ToBits(), Is.EqualTo(ulong.MaxValue));
            Assert.That(default(LongCode).ToBits(), Is.Zero);
        }

        [Test]
        public void ULongBitsPreserveTheFullWidth()
        {
            Assert.That(ULongCode.High.ToBits(), Is.EqualTo(0x8000000000000000ul));
            Assert.That(ULongCode.All.ToBits(), Is.EqualTo(ulong.MaxValue));
            Assert.That(default(ULongCode).ToBits(), Is.Zero);
        }

        [Test]
        [NonParallelizable]
        public void EnumConversionAllocatesNoManagedMemory()
        {
            var sum = 0ul;
            for (int i = 0; i < 2000; i++) sum += ConvertBits(i);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) sum += ConvertBits(i);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
            Assert.That(sum, Is.GreaterThan(0ul));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ulong ConvertBits(int value) =>
            ((ByteCode)value).ToBits()
            + ((SByteCode)value).ToBits()
            + ((ShortCode)value).ToBits()
            + ((UShortCode)value).ToBits()
            + ((IntCode)value).ToBits()
            + ((UIntCode)value).ToBits()
            + ((LongCode)value).ToBits()
            + ((ULongCode)value).ToBits();
    }
}
