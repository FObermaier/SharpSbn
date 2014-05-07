#define VERBOSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SbnSharp
{
    public class Sbx
    {
        private readonly SbnHeader _sbxHeader = new SbnHeader();

        public Sbx(BinaryReader reader)
        {
            _sbxHeader .Read(reader);
            var i = 1;
            while (reader.BaseStream.Position < _sbxHeader.FileLength)
            {
                Console.WriteLine("{0,5} {1,6} {2,6}", i++, reader.ReadInt32BE(), reader.ReadInt32BE());
            }
        }
    }

    public class Sbn
    {
        //private FileStream _sbn;
 
        private readonly List<OldSbnBin> _bins = new List<OldSbnBin>();
        private OldSbnBin[] nodeToBin;
        private readonly SbnHeader _header = new SbnHeader();

        /// <summary>
        /// Gets the number of features in this sbn
        /// </summary>
        public int NumFeatures { get; private set; }

        public static Sbn Load(string sbnFile)
        {
            if (string.IsNullOrEmpty(sbnFile))
                throw new ArgumentNullException(sbnFile);

            if (!File.Exists(sbnFile))
                throw new FileNotFoundException("File not found", sbnFile);

            return Load(new FileStream(sbnFile, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        public static Sbn Load(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            using (var reader = new BinaryReader(stream))
                return new Sbn(reader);
        }

        public Sbn(BinaryReader reader)
        {
            NumFeatures = 0;
            Read(reader);
        }

        private int GetNumBinHeaderRecords()
        {
            return nodeToBin.Length - 1;
            /*
            var count = 0;
            foreach (var bin in _bins.Skip(1))
            {
                count += 1;
                var numFeatures = 100;
                while (numFeatures < bin.NumFeatures)
                {
                    count += 1;
                    numFeatures += 100;
                }
            }
            return count;
             */
        }

        private void Read(BinaryReader reader)
        {

            var stream = reader.BaseStream;

            // Read sbn header
            _header.Read(reader);
                //var sr = new BinaryReader(_sbn);
                //fileCode = sr.ReadInt32();
                //unknownByte4 = sr.ReadInt32();
                //unused1 = sr.ReadInt32();
                //unused2 = sr.ReadInt32();
                //unused3 = sr.ReadInt32();
                //unused4 = sr.ReadInt32();
                //fileLen = sr.ReadInt32()*2;
                ////NOTE: numRecords is records in shp/dbf file not # of bins!
                //xmin = sr.ReadDouble();
                //xmax = sr.ReadDouble();
                //ymin = sr.ReadDouble();
                //ymax = sr.ReadDouble();
                //zmin = sr.ReadDouble();
                //zmax = sr.ReadDouble();
                //mmin = sr.ReadDouble();
                //mmax = sr.ReadDouble();

                var recNum = reader.ReadUInt32BE();
                var recLen = reader.ReadInt32BE() * 2;
            var numNodes = recLen/8;
            nodeToBin = new OldSbnBin[numNodes+1];

            // Add convenience bin    
            var b = new OldSbnBin(1);
            _bins.Add(b);
            var count = 0;
                for (var i = 0; i < numNodes; i++)
                {
                    b = new OldSbnBin(reader);
                    //b.Bid = sr.ReadUInt32();
                    //b.NumFeatures = sr.ReadInt32();
                    if (b.Bid > 0)
                    {
                        nodeToBin[i + 2] = b;
                        _bins.Add(b);
                        NumFeatures += b.NumFeatures;
                    }
                    else
                    {
                        count++;
                    }
#if VERBOSE
                    Console.WriteLine("{0,6} {1, 4}", b.Bid, b.NumFeatures);
#endif
                }

#if VERBOSE
                Console.WriteLine("");
#endif

            //_bins.Sort();
                while (stream.Position < _header.FileLength)
                {
                    var binId = reader.ReadInt32BE();
#if VERBOSE
                    Console.Write("{0, 6}", binId);
#endif
                    b = GetBin(binId);
                    if (b == null)
                        throw new SbnException("Bin with id " + binId + " not found");

                    b.ReadContent(reader);

                    //recLen = reader.ReadInt32BE() * 2;
                    //for (var i = 0; i < recLen / 8; i++)
                    //{
                    //    var f = new SbnFeature(sr);
                    //    /*
                    //    var f = new Feature();
                    //    f.xmin = sr.ReadInt32();
                    //    f.xmax = sr.ReadInt32();
                    //    f.xmin = sr.ReadInt32();
                    //    f.ymax = sr.ReadInt32();
                    //    f.id = sr.ReadUInt32();
                    //     */

                    //    b = bin(binId);
                    //    b.Features.Add(f);
                    //    numFeat++;
                    //}
                
                //_sbn.Close();
                

            }

            var count1 = 0;
            var count2 = 0;
            foreach (var bin in _bins)
            {
                count1 += bin.NumFeatures;
                count2 += bin.Features.Count;
                if (count1 != count2)
                {
                    break;
                }
            }

        }

        internal OldSbnBin GetBin(int binId)
        {
            while (true)
            {
                var res = _bins.Find(t => t.Bid == binId);
                if (res != null) return res;
                binId = binId - 1;
            }
        }

        public void Save(string sbnName)
        {
            if (string.IsNullOrEmpty(sbnName))
                throw new ArgumentNullException("sbnName");

            var sbxName = Path.ChangeExtension(sbnName, "sbx");

            if (File.Exists(sbnName)) File.Delete(sbnName);
            if (File.Exists(sbxName)) File.Delete(sbxName);

            using (var sbnStream = new FileStream(sbnName, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sbnWriter = new BinaryWriter(sbnStream))
            using (var sbxStream = new FileStream(sbxName, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sbxWriter = new BinaryWriter(sbxStream))
                Write(sbnWriter, sbxWriter);
        }

        private void Write(BinaryWriter sbnsw, BinaryWriter sbxsw)
        {
            // Calculate File Length fields
            // first bin descriptors
            var numBinHeaderRecords = GetNumBinHeaderRecords();
            var totalBinSize = (_bins.Count - 1) * 8;

            // then bins with features
            var usedBinSize = _bins.Count(b => b.Bid > 0) * 8;
            var sbxSize = 100 + usedBinSize;
            var sbnSize = 100 + totalBinSize + usedBinSize;
            sbnSize += NumFeatures * 8;

            // Write headers
            _header.Write(sbnsw, sbnSize);
            _header.Write(sbxsw, sbxSize);

            //sbnsw.Write(fileCode);
            //sbxsw.Write(fileCode);
            //sbnsw.Write(unknownByte4);
            //sbxsw.Write(unknownByte4);
            //sbnsw.Write(unused1);
            //sbxsw.Write(unused1);
            //sbnsw.Write(unused2);
            //sbxsw.Write(unused2);
            //sbnsw.Write(unused3);
            //sbxsw.Write(unused3);
            //sbnsw.Write(unused4);
            //sbxsw.Write(unused4);

            //// Calculate File Length fields
            //// first bin descriptors
            //var totalBinSize = (_bins.Count - 1)*8;

            //// then bins with features
            //var usedBinSize = _bins.Count(b => b.Bid > 0)*8;
            //var sbxSize = 100 + usedBinSize;
            //var sbnSize = 100 + totalBinSize + usedBinSize;
            //sbxSize /= 2;
            //sbnSize += NumFeatures*8;
            //sbnSize /= 2;

            //sbnsw.Write(sbnSize);
            //sbxsw.Write(sbxSize);
            //sbnsw.Write(numRecords);
            //sbxsw.Write(numRecords);
            //sbnsw.Write(xmin);
            //sbxsw.Write(xmin);
            //sbnsw.Write(ymin);
            //sbxsw.Write(ymin);
            //sbnsw.Write(xmax);
            //sbxsw.Write(xmax);
            //sbnsw.Write(ymax);
            //sbxsw.Write(ymax);
            //sbnsw.Write(zmin);
            //sbxsw.Write(zmin);
            //sbnsw.Write(zmax);
            //sbxsw.Write(zmax);
            //sbnsw.Write(mmin);
            //sbxsw.Write(mmin);
            //sbnsw.Write(mmax);
            //sbxsw.Write(mmax);
            //sbnsw.Write(unknownByte96);
            //sbxsw.Write(unknownByte96);

            // sbn and sbx records

            // first create bin descriptors record
            var recLen = (numBinHeaderRecords) * 4;
            sbnsw.WriteBE(1);
            sbnsw.WriteBE(recLen);

            sbxsw.WriteBE(100/2);
            sbxsw.WriteBE(recLen + 2);
            for (var i = 1; i < nodeToBin.Length; i++)
            {
                if (nodeToBin[i] == null)
                {
                    sbnsw.Write(-1);
                    sbnsw.Write(0);
                }
                else
                {
                    nodeToBin[i].WriteHeader(sbnsw);
                }
            }
            /*
            foreach (var b in _bins.Skip(1))
            {
                
                b.WriteHeader(sbnsw);
            }*/

            // write actual bins
            foreach (var b in _bins.Skip(1))
            {
                if (b.Bid <= 0) continue;

                //sbxsw.WriteBE((int) sbnsw.BaseStream.Position/2);
                //sbxsw.WriteBE(b.NumFeatures*2);
                
                b.Write(sbnsw, sbxsw);

                //sbnsw.Write(b.Bid);
                //sbnsw.Write(b.NumFeatures*4);

                //foreach (var f in b.Features)
                //{
                //    f.Write(sbnsw);
                //    //sbnsw.Write(f.xmin);
                //    //sbnsw.Write(f.ymin);
                //    //sbnsw.Write(f.xmax);
                //    //sbnsw.Write(f.ymax);
                //    //sbnsw.Write(f.id);
                //}
            }
            //sbn.Close();
            //sbx.Close();

        }

        //private int fileCode;
        //private int unknownByte4;
        //private int unused1;
        //private int unused2;
        //private int unused3;
        //private int unused4;
        //private int fileLen;
        //private int numRecords;
        //private double xmin, xmax;
        //private double ymin, ymax;
        //private double zmin, zmax;
        //private double mmin, mmax;
        //private int unknownByte96;
    }
}