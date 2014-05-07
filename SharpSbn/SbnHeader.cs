using System.IO;
using System.Text;
using GeoAPI.DataStructures;
using GeoAPI.Geometries;

namespace SharpSbn
{
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
        /// Gets an envelope of the area covered by this index
        /// </summary>
        internal Envelope Envelope { get { return new Envelope(XRange.Min, XRange.Max, YRange.Min, YRange.Max); } }

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
            var fileCode = reader.ReadInt32BE();
            var fileCodeIndex = reader.ReadInt32BE();
            if (fileCode != FileCode || fileCodeIndex != FileCodeIndex)
                throw new SbnException("Not a Shapefile index file");

            reader.BaseStream.Seek(16, SeekOrigin.Current);

            FileLength = reader.ReadInt32BE() * 2;
            NumRecords = reader.ReadInt32BE();

            var minX = reader.ReadDoubleBE();
            var minY = reader.ReadDoubleBE();
            XRange = Interval.Create(minX, reader.ReadDoubleBE());
            YRange = Interval.Create(minY, reader.ReadDoubleBE());
            ZRange = Interval.Create(reader.ReadDoubleBE(), reader.ReadDoubleBE());
            MRange = Interval.Create(reader.ReadDoubleBE(), reader.ReadDoubleBE());

            reader.BaseStream.Seek(4, SeekOrigin.Current);
        }

        /// <summary>
        /// Method to write the index header using the provided writer
        /// </summary>
        /// <param name="writer">The writer to use</param>
        /// <param name="fileLength"></param>
        internal void Write(BinaryWriter writer, int? fileLength = null)
        {
            writer.WriteBE(FileCode);
            writer.WriteBE(FileCodeIndex);

            writer.Write(new byte[16]);

            writer.WriteBE((fileLength ?? FileLength) / 2);
            writer.WriteBE(NumRecords);

            writer.WriteBE(XRange.Min);
            writer.WriteBE(YRange.Min);
            writer.WriteBE(XRange.Max);
            writer.WriteBE(YRange.Max);

            writer.WriteBE(ZRange);
            writer.WriteBE(MRange);

            writer.Write(0);
        }

        internal void AddFeature(uint id, IGeometry geometry)
        {
            NumRecords++;
        }

        internal void RemoveFeature()
        {
            NumRecords--;
        }

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