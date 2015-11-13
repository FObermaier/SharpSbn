using System.IO;
using System.Text;
#if UseGeoAPI
using Interval = GeoAPI.DataStructures.Interval;
using Envelope = GeoAPI.Geometries.Envelope;
#else
using Interval = SharpSbn.DataStructures.Interval;
using Envelope = SharpSbn.DataStructures.Envelope;
#endif
namespace SharpSbn
{
    /// <summary>
    /// A class containing ESRI SBN Index information
    /// </summary>
    public class SbnHeader
    {
        /// <summary>
        /// A magic number identifying this file as belonging to the ShapeFile family
        /// </summary>
        private const int FileCode = 9994;

        /// <summary>
        /// (Assumption)
        /// A magic number identifying this file is an index, not a shapefile (assumption)
        /// </summary>
        private const int FileCodeIndex = -400;

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        public SbnHeader()
        {
            FileLength = 100;
            NumRecords = 0;
            XRange = Interval.Create();
            YRange = Interval.Create();
            ZRange = Interval.Create();
            MRange = Interval.Create();
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="numRecords">The number of features</param>
        /// <param name="extent">The extent</param>
        public SbnHeader(int numRecords, Envelope extent)
        {
            NumRecords = numRecords;
            XRange = Interval.Create(extent.MinX, extent.MaxX);
            YRange = Interval.Create(extent.MinY, extent.MaxY);
            ZRange = Interval.Create();
            MRange = Interval.Create();
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="numRecords">The number of features</param>
        /// <param name="xInterval">The x-Oridnate extent</param>
        /// <param name="yInterval">The y-Oridnate extent</param>
        /// <param name="zInterval">The z-Oridnate extent</param>
        /// <param name="mInterval">The m-Oridnate extent</param>
        public SbnHeader(int numRecords, Interval xInterval, Interval yInterval, Interval zInterval, Interval mInterval)
        {
            NumRecords = numRecords;
            XRange = xInterval;
            YRange = yInterval;
            ZRange = zInterval;
            MRange = mInterval;
        }

        /// <summary>
        /// Gets the number of records in this index (Features in the shapefile)
        /// </summary>
        public int NumRecords { get; private set; }

        /// <summary>
        /// Gets the length of the index file in bytes
        /// </summary>
        public int FileLength { get; private set; }

        /// <summary>
        /// Gets a value indicating the area covered by this index
        /// </summary>
        internal Envelope Extent 
        {
            get
            {
                return new Envelope(XRange.Min, XRange.Max, 
                                    YRange.Min, YRange.Max);
            } 
        }

        /// <summary>
        /// Gets the x-ordinate range covered by this index
        /// </summary>
        public Interval XRange { get; private set; }

        /// <summary>
        /// Gets the y-ordinate range covered by this index
        /// </summary>
        public Interval YRange { get; private set; }

        /// <summary>
        /// Gets the z-ordinate range covered by this index
        /// </summary>
        public Interval ZRange { get; private set; }

        /// <summary>
        /// Gets the m-ordinate range covered by this index
        /// </summary>
        public Interval MRange { get; private set; }

        /// <summary>
        /// Method to read the index header using the provided reader
        /// </summary>
        /// <param name="reader">The reader to use</param>
        public void Read(BinaryReader reader)
        {
            var fileCode = BinaryIOExtensions.ReadInt32BE(reader);
            var fileCodeIndex = BinaryIOExtensions.ReadInt32BE(reader);
            if (fileCode != FileCode || fileCodeIndex != FileCodeIndex)
                throw new SbnException("Not a Shapefile index file");

            reader.BaseStream.Seek(16, SeekOrigin.Current);

            FileLength = BinaryIOExtensions.ReadInt32BE(reader) * 2;
            NumRecords = BinaryIOExtensions.ReadInt32BE(reader);

            var minX = BinaryIOExtensions.ReadDoubleBE(reader);
            var minY = BinaryIOExtensions.ReadDoubleBE(reader);
            XRange = Interval.Create(minX, BinaryIOExtensions.ReadDoubleBE(reader));
            YRange = Interval.Create(minY, BinaryIOExtensions.ReadDoubleBE(reader));
            ZRange = Interval.Create(BinaryIOExtensions.ReadDoubleBE(reader), BinaryIOExtensions.ReadDoubleBE(reader));
            MRange = Interval.Create(BinaryIOExtensions.ReadDoubleBE(reader), BinaryIOExtensions.ReadDoubleBE(reader));

            reader.BaseStream.Seek(4, SeekOrigin.Current);
        }

        /// <summary>
        /// Method to write the index header using the provided writer
        /// </summary>
        /// <param name="writer">The writer to use</param>
        /// <param name="fileLength"></param>
        internal void Write(BinaryWriter writer, int? fileLength = null)
        {
            BinaryIOExtensions.WriteBE(writer, FileCode);
            BinaryIOExtensions.WriteBE(writer, FileCodeIndex);

            writer.Write(new byte[16]);

            BinaryIOExtensions.WriteBE(writer, (fileLength ?? FileLength) / 2);
            BinaryIOExtensions.WriteBE(writer, NumRecords);

            BinaryIOExtensions.WriteBE(writer, XRange.Min);
            BinaryIOExtensions.WriteBE(writer, YRange.Min);
            BinaryIOExtensions.WriteBE(writer, XRange.Max);
            BinaryIOExtensions.WriteBE(writer, YRange.Max);

            BinaryIOExtensions.WriteBE(writer, ZRange);
            BinaryIOExtensions.WriteBE(writer, MRange);

            writer.Write(0);
        }

        internal void AddFeature(uint id, Envelope geometry, Interval? zRange, Interval? mRange)
        {
            NumRecords++;
            XRange = XRange.ExpandedByInterval(Interval.Create(geometry.MinX, geometry.MaxX));
            YRange = YRange.ExpandedByInterval(Interval.Create(geometry.MinY, geometry.MaxY));
            ZRange = ZRange.ExpandedByInterval(zRange ?? Interval.Create());
            MRange = MRange.ExpandedByInterval(mRange ?? Interval.Create());
        }

        internal void RemoveFeature()
        {
            NumRecords--;
        }

        /// <summary>
        /// Method to print out this <see cref="SbnHeader"/>
        /// </summary>
        /// <returns>
        /// A text describing this <see cref="SbnHeader"/>
        /// </returns>
        public override string ToString()
        {
            var res = new StringBuilder();
            res.AppendLine("[SbnHeader");
            res.AppendFormat("  FileCode: {0}\n", FileCode);
            res.AppendFormat("  FileCode2: {0}\n", FileCodeIndex);
            res.AppendFormat("  NumRecords: {0}\n", NumRecords);
            res.AppendFormat("  FileLength: {0}\n", FileLength);
            res.AppendFormat("  XRange: {0}\n", XRange);
            res.AppendFormat("  YRange: {0}\n", YRange);
            res.AppendFormat("  ZRange: {0}\n", ZRange);
            res.AppendFormat("  MRange: {0}]", MRange);
            return res.ToString();
        }
    }
}