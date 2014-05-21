using GeoAPI.DataStructures;
using System;
using System.IO;

namespace SharpSbn
{
    internal static class BinaryIOExtensions
    {
        // ReSharper disable InconsistentNaming
        internal static Int32 ReadInt32BE(this BinaryReader self)
        {
            var buffer = self.ReadBytes(4);
            Array.Reverse(buffer);
            return BitConverter.ToInt32(buffer, 0);
        }

        internal static UInt32 ReadUInt32BE(this BinaryReader self)
        {
            var buffer = self.ReadBytes(4);
            Array.Reverse(buffer);
            return BitConverter.ToUInt32(buffer, 0);
        }

        internal static Double ReadDoubleBE(this BinaryReader self)
        {
            var buffer = self.ReadBytes(8);
            Array.Reverse(buffer);
            return BitConverter.ToDouble(buffer, 0);
        }

        internal static void WriteBE(this BinaryWriter self, Int32 value)
        {
            var buffer = BitConverter.GetBytes(value);
            Array.Reverse(buffer);
            self.Write(buffer);
        }

        internal static void WriteBE(this BinaryWriter self, UInt32 value)
        {
            var buffer = BitConverter.GetBytes(value);
            Array.Reverse(buffer);
            self.Write(buffer);
        }

        internal static void WriteBE(this BinaryWriter self, Double value)
        {
            var buffer = BitConverter.GetBytes(value);
            Array.Reverse(buffer);
            self.Write(buffer);
        }

        internal static void WriteBE(this BinaryWriter self, Interval value)
        {
            self.WriteBE(value.Min);
            self.WriteBE(value.Max);
        }

        // ReSharper restore InconsistentNaming
    }
}