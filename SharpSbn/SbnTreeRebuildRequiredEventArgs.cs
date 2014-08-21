using System;

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
        [CLSCompliant(false)]
        public SbnTreeRebuildRequiredEventArgs(uint fid, object geometry)
        {
            Fid = fid;
            Geometry = geometry;
        }

        /// <summary>
        /// The feature's id
        /// </summary>
        [CLSCompliant(false)]
        public uint Fid { get; private set; }

        /// <summary>
        /// Gets a value indicating the geometry's 
        /// </summary>
        public object Geometry { get; private set; }
    }
}