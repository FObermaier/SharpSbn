#if UseGeoAPI
using Interval = GeoAPI.DataStructures.Interval;
#else
using Interval = SharpSbn.DataStructures.Interval;
#endif
using System;
using System.IO;

namespace SharpSbn
{
    internal static class BinaryIOExtensions
    {
        // ReSharper disable InconsistentNaming
        internal static Int32 ReadInt32BE(BinaryReader self)
        {
            var buffer = self.ReadBytes(4);
            Array.Reverse(buffer);
            return BitConverter.ToInt32(buffer, 0);
        }

        internal static UInt32 ReadUInt32BE(BinaryReader self)
        {
            var buffer = self.ReadBytes(4);
            Array.Reverse(buffer);
            return BitConverter.ToUInt32(buffer, 0);
        }

        internal static Double ReadDoubleBE(BinaryReader self)
        {
            var buffer = self.ReadBytes(8);
            Array.Reverse(buffer);
            return BitConverter.ToDouble(buffer, 0);
        }

        internal static void WriteBE(BinaryWriter self, Int32 value)
        {
            var buffer = BitConverter.GetBytes(value);
            Array.Reverse(buffer);
            self.Write(buffer);
        }

        internal static void WriteBE(BinaryWriter self, UInt32 value)
        {
            var buffer = BitConverter.GetBytes(value);
            Array.Reverse(buffer);
            self.Write(buffer);
        }

        internal static void WriteBE(BinaryWriter self, Double value)
        {
            var buffer = BitConverter.GetBytes(value);
            Array.Reverse(buffer);
            self.Write(buffer);
        }

        internal static void WriteBE(BinaryWriter self, Interval value)
        {
            WriteBE(self, value.Min);
            WriteBE(self, value.Max);
        }

        // ReSharper restore InconsistentNaming
    }
}