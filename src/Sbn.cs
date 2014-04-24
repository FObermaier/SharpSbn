using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SbnIndex
{
    public class Sbn
    {
        private FileStream _sbn;
 
        public List<Bin> bins = new List<Bin>();
        
        public int numFeat;
        public string sbnName;

        public Sbn(string sbn)
        {
            numFeat = 0;
            sbnName = sbn;
            Load(sbnName);
        }

        public void Load(string filename)
        {
            _sbn = null;
            fileCode = 9994;
            unknownByte4 = -400;
            unused1 = 0;
            unused2 = 0;
            unused3 = 0;
            unused4 = 0;
            fileLen = 0;
            numRecords = 0;
            xmin = 0d;
            ymin = 0d;
            xmax = 0d;
            ymax = 0d;
            zmin = 0d;
            zmax = 0d;
            zmax = 0d;
            mmin = 0d;
            mmax = 0d;
            unknownByte96 = 0;

            if (!string.IsNullOrEmpty(sbnName))
            {
                try
                {
                    _sbn = new FileStream(sbnName, FileMode.Open);
                }
                catch (Exception ex)
                {
                    throw new SbnException("Unable to open sbn file or sbn file not specified", ex);
                }

                // Read sbn header
                var sr = new BinaryReader(_sbn);
                fileCode = sr.ReadInt32();
                unknownByte4 = sr.ReadInt32();
                unused1 = sr.ReadInt32();
                unused2 = sr.ReadInt32();
                unused3 = sr.ReadInt32();
                unused4 = sr.ReadInt32();
                fileLen = sr.ReadInt32()*2;
                //NOTE: numRecords is records in shp/dbf file not # of bins!
                xmin = sr.ReadDouble();
                xmax = sr.ReadDouble();
                ymin = sr.ReadDouble();
                ymax = sr.ReadDouble();
                zmin = sr.ReadDouble();
                zmax = sr.ReadDouble();
                mmin = sr.ReadDouble();
                mmax = sr.ReadDouble();

                var recNum = sr.ReadUInt32();
                var recLen = sr.ReadInt32();

                var b = new Bin();
                b.id = sr.ReadUInt32();
                b.numFeat = sr.ReadInt32();
                bins.Add(b);
                for (var i = 0; i < recLen/8; i++)
                {
                    b = new Bin();
                    b.id = sr.ReadUInt32();
                    b.numFeat = sr.ReadInt32();
                    bins.Add(b);
                }

                while (_sbn.Position < fileLen)
                {
                    var binId = sr.ReadUInt32();
                    recLen = sr.ReadInt32() * 2;
                    for (var i = 0; i < recLen / 8; i++)
                    {
                        var f = new Feature(sr);
                        /*
                        var f = new Feature();
                        f.xmin = sr.ReadInt32();
                        f.xmax = sr.ReadInt32();
                        f.xmin = sr.ReadInt32();
                        f.ymax = sr.ReadInt32();
                        f.id = sr.ReadUInt32();
                         */

                        b = bin(binId);
                        b.features.Add(f);
                        numFeat++;
                    }
                }
                _sbn.Close();
                

            }
        }

        public Bin bin(uint binId)
        {
            return bins.Find(t => t.id == binId);
        }

        public void Save(string sbnName)
        {
            sbnName = sbnName ?? this.sbnName;
            var sbn = new FileStream(sbnName, FileMode.Create);
            var sbnsw = new BinaryWriter(sbn);
            var sbxName = Path.ChangeExtension(sbnName, "sbx");
            var sbx = new FileStream(sbxName, FileMode.Create);
            var sbxsw = new BinaryWriter(sbx);

            // Write headers
            sbnsw.Write(fileCode);
            sbxsw.Write(fileCode);
            sbnsw.Write(unknownByte4);
            sbxsw.Write(unknownByte4);
            sbnsw.Write(unused1);
            sbxsw.Write(unused1);
            sbnsw.Write(unused2);
            sbxsw.Write(unused2);
            sbnsw.Write(unused3);
            sbxsw.Write(unused3);
            sbnsw.Write(unused4);
            sbxsw.Write(unused4);

            // Calculate File Length fields
            // first bin descriptors
            var totalBinSize = (bins.Count - 1)*8;

            // then bins with features
            var usedBinSize = bins.Count(b => b.id > 0)*8;
            var sbxSize = 100 + usedBinSize;
            var sbnSize = 100 + totalBinSize + usedBinSize;
            sbxSize /= 2;
            sbnSize += numFeat*8;
            sbnSize /= 2;

            sbnsw.Write(sbnSize);
            sbxsw.Write(sbxSize);
            sbnsw.Write(numRecords);
            sbxsw.Write(numRecords);
            sbnsw.Write(xmin);
            sbxsw.Write(xmin);
            sbnsw.Write(ymin);
            sbxsw.Write(ymin);
            sbnsw.Write(xmax);
            sbxsw.Write(xmax);
            sbnsw.Write(ymax);
            sbxsw.Write(ymax);
            sbnsw.Write(zmin);
            sbxsw.Write(zmin);
            sbnsw.Write(zmax);
            sbxsw.Write(zmax);
            sbnsw.Write(mmin);
            sbxsw.Write(mmin);
            sbnsw.Write(mmax);
            sbxsw.Write(mmax);
            sbnsw.Write(unknownByte96);
            sbxsw.Write(unknownByte96);

            // sbn and sbx records

            // first create bin descriptors record
            var recLen = (bins.Count - 1)*4;
            sbnsw.Write(1);
            sbnsw.Write(recLen);
            sbxsw.Write(100);
            sbxsw.Write(recLen + 2);
            foreach (var b in bins.Skip(1))
            {
                sbnsw.Write(b.id);
                sbnsw.Write(b.features.Count);
            }

            // write actual bins
            foreach (var b in bins.Skip(1))
            {
                if (b.id <= 0) continue;

                sbxsw.Write((int) sbn.Position);
                sbxsw.Write(b.features.Count*2);
                sbnsw.Write(b.id);
                sbnsw.Write(b.features.Count*4);

                foreach (var f in b.features)
                {
                    sbnsw.Write(f.xmin);
                    sbnsw.Write(f.ymin);
                    sbnsw.Write(f.xmax);
                    sbnsw.Write(f.ymax);
                    sbnsw.Write(f.id);
                }
            }
            sbn.Close();
            sbx.Close();

        }

        private int fileCode;
        private int unknownByte4;
        private int unused1;
        private int unused2;
        private int unused3;
        private int unused4;
        private int fileLen;
        private int numRecords;
        private double xmin, xmax;
        private double ymin, ymax;
        private double zmin, zmax;
        private double mmin, mmax;
        private int unknownByte96;
    }
}