using System;
using System.IO;
using System.Runtime.InteropServices;
using GeoAPI.DataStructures;
using GeoAPI.Geometries;

namespace SbnSharp
{
    /// <summary>
    /// An entry in the Sbn index 
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct SbnFeature : IEquatable<SbnFeature>
    {
        [FieldOffset(4)]
        private readonly uint _fid;

        [FieldOffset(0)]
        internal readonly byte MinX;
        [FieldOffset(1)]
        internal readonly byte MaxX;
        [FieldOffset(2)]
        internal readonly byte MinY;
        [FieldOffset(3)]
        internal readonly byte MaxY;

        /// <summary>
        /// Creates an instance of this class using a binary reader
        /// </summary>
        /// <param name="sr">The binary reader</param>
        internal SbnFeature(BinaryReader sr)
        {
            MinX = sr.ReadByte();
            MinY = sr.ReadByte();
            MaxX = sr.ReadByte();
            MaxY = sr.ReadByte();
            _fid = sr.ReadUInt32BE();
        }

        /// <summary>
        /// Creates an instance of this class using the provided <paramref name="fid"/> and <paramref name="extent"/>
        /// </summary>
        /// <param name="header">The header of the index</param>
        /// <param name="fid">The feature's id</param>
        /// <param name="extent">The feature's extent</param>
        public SbnFeature(SbnHeader header, uint fid, Envelope extent)
        {
            _fid = fid;
            MinX = ScaleLower(extent.MinX, header.XRange);
            MinY = ScaleLower(extent.MinY, header.YRange);
            MaxX = ScaleUpper(extent.MaxX, header.XRange);
            MaxY = ScaleUpper(extent.MaxY, header.YRange);
        }

        /// <summary>
        /// Creates an instance of this class using the provided <paramref name="fid"/> and <paramref name="extent"/>
        /// </summary>
        /// <param name="sfExtent">The extent of the index</param>
        /// <param name="fid">The feature's id</param>
        /// <param name="extent">The feature's extent</param>
        public SbnFeature(Envelope sfExtent, uint fid, Envelope extent)
        {
            _fid = fid;
            var xrange = Interval.Create(sfExtent.MinX, sfExtent.MaxX);
            var yrange = Interval.Create(sfExtent.MinY, sfExtent.MaxY);

            MinX = ScaleLower(extent.MinX, xrange);
            MinY = ScaleLower(extent.MinY, yrange);
            MaxX = ScaleUpper(extent.MaxX, xrange);
            MaxY = ScaleUpper(extent.MaxY, yrange);
        }

        public SbnFeature(byte[] featureBytes)
        {
            MinX = featureBytes[0];
            MinY = featureBytes[1];
            MaxX = featureBytes[2];
            MaxY = featureBytes[3];
            _fid = BitConverter.ToUInt32(featureBytes, 4);
        }

        ///// <summary>
        ///// Method to get an approximate extent of the this feature
        ///// </summary>
        //public Envelope GetExtent(SbnHeader header)
        //{
        //    return new Envelope(
        //        header.XRange.Min + MinX*header.XRange.Width/255d,
        //        header.XRange.Max + MaxX*header.XRange.Width/255d,
        //        header.YRange.Min + MinY*header.YRange.Width/255d,
        //        header.YRange.Max + MaxY*header.YRange.Width/255d);
        //}

        /// <summary>
        /// Intersection predicate
        /// </summary>
        /// <param name="minX">The lower x-ordinate</param>
        /// <param name="maxX">The upper x-ordinate</param>
        /// <param name="minY">The lower y-ordinate</param>
        /// <param name="maxY">The upper y-ordinate</param>
        /// <returns><value>true</value> if this feature intersects the bounds</returns>
        public bool Intersects(byte minX, byte maxX, byte minY, byte maxY)
        {
            return !(minX > MaxX || maxX < MinX || minY > MaxY || maxY < MinY);
        }

        /// <summary>
        /// Gets the id of this feature
        /// </summary>
        public uint Fid { get { return _fid; }}

        /// <summary>
        /// Method to write the feature to an index
        /// </summary>
        /// <param name="writer"></param>
        internal void Write(BinaryWriter writer)
        {
            writer.Write(MinX);
            writer.Write(MinY);
            writer.Write(MaxX);
            writer.Write(MaxY);
            writer.WriteBE(_fid);
        }

        #region private utility functions
        private static byte ScaleLower(double value, Interval range)
        {
            var min = ((value - range.Min) / range.Width * 255.0);
            // not sure why this rounding is needed, but it is
            var modMin = (min % 1 - .005) % 1 + (int)min;
            var res = (int)(Math.Floor(modMin));
            if (res < 0) res = 0;
            return (byte)res;
        }

        private static byte ScaleUpper(double value, Interval range)
        {
            var max = ((value - range.Min) / range.Width * 255.0);
            var modMax = (max % 1 + .005) % 1 + (int)max;
            var res = (int)Math.Ceiling(modMax);
            if (res > 255) res = 255;
            return (byte)res;
        }
        #endregion

        public bool Equals(SbnFeature other)
        {
            return other.Fid == _fid;
        }

        public override bool Equals(object other)
        {
            if (other is SbnFeature)
                return Equals((SbnFeature)other);
            return false;
        }

        public override int GetHashCode()
        {
            return 31 * typeof(SbnFeature).GetHashCode() * _fid.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("[SbnFeature {0}: ({1},{2},{3},{4})]", _fid, MinX, MaxX, MinY, MaxY);
        }

        public Array AsBytes()
        {
            var res = new byte[8];
            res[0] = MinX;
            res[1] = MinY;
            res[2] = MaxX;
            res[3] = MaxY;
            Buffer.BlockCopy(BitConverter.GetBytes(Fid), 0, res, 4, 4);
            return res;
        }
    }
}