using System;
#if (UseGeoAPI)
using Envelope = GeoAPI.Geometries.Envelope;
using Interval = GeoAPI.DataStructures.Interval;
#else
using Envelope = SharpSbn.DataStructures.Envelope;
using Interval = SharpSbn.DataStructures.Interval;
#endif
namespace SharpSbn
{
    /// <summary>
    /// A class containing the id and the geometry of a feature that causes the rebuild required event
    /// </summary>
    public class SbnTreeRebuildRequiredEventArgs : EventArgs
    {
        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="fid">The feature's id</param>
        /// <param name="geometry">The features geometry</param>
        /// <param name="zRange">An optional value for the z-Range</param>
        /// <param name="mRange">An optional value for the m-Range</param>
        [CLSCompliant(false)]
        public SbnTreeRebuildRequiredEventArgs(uint fid, Envelope geometry, Interval? zRange, Interval? mRange)
        {
            Fid = fid;
            Geometry = geometry;
            ZRange = zRange;
            MRange = mRange;
        }

        /// <summary>
        /// The feature's id
        /// </summary>
        [CLSCompliant(false)]
        public uint Fid { get; private set; }

        /// <summary>
        /// Gets a value indicating the geometry's 
        /// </summary>
        public Envelope Geometry { get; private set; }

        /// <summary>
        /// Gets a value indicating the geometry's 
        /// </summary>
        public Interval? ZRange { get; private set; }

        /// <summary>
        /// Gets a value indicating the geometry's 
        /// </summary>
        public Interval? MRange { get; private set; }
    }
}