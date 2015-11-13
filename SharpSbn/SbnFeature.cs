using System;
using System.IO;
using System.Runtime.InteropServices;
#if UseGeoAPI
using Envelope = GeoAPI.Geometries.Envelope;
#else
using Envelope = SharpSbn.DataStructures.Envelope;
#endif

namespace SharpSbn
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
            _fid = BinaryIOExtensions.ReadUInt32BE(sr);
        }

        /// <summary>
        /// Creates an instance of this class using the provided <paramref name="fid"/> and <paramref name="extent"/>
        /// </summary>
        /// <param name="header">The header of the index</param>
        /// <param name="fid">The feature's id</param>
        /// <param name="extent">The feature's extent</param>
#pragma warning disable 3001
        public SbnFeature(SbnHeader header, uint fid, Envelope extent)
            :this(header.Extent, fid, extent)
        {
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
            ClampUtility.Clamp(sfExtent, extent, out MinX, out MinY, out MaxX, out MaxY);
        }
#pragma warning restore 3001

        //public SbnFeature(byte[] featureBytes)
        //{
        //    MinX = featureBytes[0];
        //    MinY = featureBytes[1];
        //    MaxX = featureBytes[2];
        //    MaxY = featureBytes[3];
        //    _fid = BitConverter.ToUInt32(featureBytes, 4);
        //}

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
        internal bool Intersects(byte minX, byte maxX, byte minY, byte maxY)
        {
            return !(minX > MaxX || maxX < MinX || minY > MaxY || maxY < MinY);
        }

        /// <summary>
        /// Gets the id of this feature
        /// </summary>
        [CLSCompliant(false)]
        public uint Fid { get { return _fid; } }

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
            BinaryIOExtensions.WriteBE(writer, _fid);
        }

        /// <summary>
        /// Function to test if this <see cref="SbnFeature"/> equals <paramref name="other"/>
        /// </summary>
        /// <param name="other">The other feature</param>
        /// <returns><value>true</value> if the this <see cref="Fid"/> equals <paramref name="other"/>'s <see cref="Fid"/></returns>
        public bool Equals(SbnFeature other)
        {
            return other.Fid == _fid;
        }

        /// <summary>
        /// Function to test if this <see cref="SbnFeature"/> equals <paramref name="other"/>
        /// </summary>
        /// <param name="other">The other feature</param>
        /// <returns><value>true</value> if the this <see cref="Fid"/> equals <paramref name="other"/>'s <see cref="Fid"/></returns>
        public override bool Equals(object other)
        {
            if (other is SbnFeature)
                return Equals((SbnFeature)other);
            return false;
        }

        /// <summary>
        /// Function to return the hashcode for this object.
        /// </summary>
        /// <returns>
        /// A hashcode
        /// </returns>
        public override int GetHashCode()
        {
            return 31 * typeof(SbnFeature).GetHashCode() * _fid.GetHashCode();
        }

        /// <summary>
        /// Method to print out this <see cref="SbnFeature"/>
        /// </summary>
        /// <returns>A text describing this <see cref="SbnFeature"/></returns>
        public override string ToString()
        {
            return string.Format("[SbnFeature {0}: ({1}-{2},{3}-{4})]", _fid, MinX, MaxX, MinY, MaxY);
        }

        /// <summary>
        /// Method to convert the feature to a byte array
        /// </summary>
        /// <returns>An array of bytes</returns>
        internal Array AsBytes()
        {
            var res = new byte[8];
            res[0] = MinX;
            res[1] = MinY;
            res[2] = MaxX;
            res[3] = MaxY;
            Buffer.BlockCopy(BitConverter.GetBytes(Fid), 0, res, 4, 4);
            return res;
        }

        /// <summary>
        /// An operator for equuality comarison
        /// </summary>
        /// <param name="lhs">The value on the left-hand-side</param>
        /// <param name="rhs">The value on the right-hand-side</param>
        /// <returns><value>true</value> if <paramref name="lhs"/> == <paramref name="rhs"/></returns>
        public static bool operator ==(SbnFeature lhs, SbnFeature rhs)
        {
            return lhs.Equals(rhs);
        }
        /// <summary>
        /// An operator for inequuality comarison
        /// </summary>
        /// <param name="lhs">The value on the left-hand-side</param>
        /// <param name="rhs">The value on the right-hand-side</param>
        /// <returns><value>true</value> if <paramref name="lhs"/> != <paramref name="rhs"/></returns>
        public static bool operator !=(SbnFeature lhs, SbnFeature rhs)
        {
            return !lhs.Equals(rhs);
        }
    }
}