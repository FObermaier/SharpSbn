using System;
using GeoAPI.Geometries;

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
        public SbnTreeRebuildRequiredEventArgs(uint fid, IGeometry geometry)
        {
            Fid = fid;
            Geometry = geometry;
        }

        /// <summary>
        /// The feature's id
        /// </summary>
        public uint Fid { get; private set; }

        /// <summary>
        /// Gets a value indicating the geometry's 
        /// </summary>
        public IGeometry Geometry { get; private set; }
    }
}