using System;
using System.Collections.Generic;
using System.IO;

namespace SharpSbn
{
    internal class SbnBin
    {
        //private readonly byte[] _buffer;
        private readonly SbnFeature[] _features;
        private SbnBin _next;

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        internal SbnBin()
        {
            //_buffer = new byte[800];
            _features = new SbnFeature[100];
        }

        /// <summary>
        /// Gets reference to a next bin
        /// </summary>
        public SbnBin Next
        {
            get { return _next; }
            internal set
            {
                _next = value;
                _next.Offset = Offset + 100;
            }
        }

        /// <summary>
        /// Gets the number of features in bin
        /// </summary>
        public int NumFeatures { get; private set; }

        /// <summary>
        /// Gets a value indicating the index offset of the featues in this node
        /// </summary>
        public int Offset { get; private set; }

        /// <summary>
        /// Gets a feature from the bin
        /// </summary>
        /// <param name="index">The index of the feature in the bin</param>
        /// <returns></returns>
        public SbnFeature this[int index]
        {
            get
            {
                if (index < 0)
                    throw new ArgumentOutOfRangeException();

                if (index < 100)
                {
                    return _features[index];
                    //var featureBytes = new byte[8];
                    //Buffer.BlockCopy(_buffer, index*8, featureBytes, 0, 8);
                    //return new SbnFeature(featureBytes);
                }

                throw new ArgumentOutOfRangeException("index");

                //if (Next == null)
                //    throw new ArgumentOutOfRangeException("index");

                //return Next[index - 100];
            }
            private set
            {
                //Buffer.BlockCopy(value.AsBytes(), 0, _buffer, index *8, 8);
                _features[index] = value;
            }
        }
        internal void AddFeature(SbnFeature feature)
        {
            this[NumFeatures++] = feature;
        }

        //internal SbnBin Clone()
        //{
        //    //return this;

        //    var res = new SbnBin { NumFeatures = NumFeatures };
        //    Buffer.BlockCopy(_buffer, 0, res._buffer, 0, 800);
        //    if (Next != null) res.Next = Next.Clone();

        //    return res;
        //}

        internal void CopyTo(SbnFeature[] res, int offset)
        {
            if (offset + NumFeatures > res.Length)
                throw new ArgumentException("Array not large enough", "res");

            for (var i = 0; i < NumFeatures; i++)
                res[offset + i] = _features[i];
        }

        internal IEnumerable<uint> GetAllFidsInBin()
        {
            var res = new uint[NumFeatures];
            for (var i = 0; i < NumFeatures; i++)
            {
                res[i] = _features[i].Fid;
                //res[i] = BitConverter.ToUInt32(_buffer, 4 + i * 8);
            }
            return res;
        }

        /// <summary>
        /// Method to read a bin
        /// </summary>
        /// <param name="reader">The reader to use</param>
        internal int Read(BinaryReader reader)
        {
            var bid = BinaryIOExtensions.ReadInt32BE(reader);
            NumFeatures = BinaryIOExtensions.ReadInt32BE(reader) / 4;
            ReadBuffer(reader);
            return bid;
        }

        internal void RemoveAt(int index)
        {
            if (index < NumFeatures - 1)
            {
                var max = NumFeatures - 1;
                for (var i = index; i < max; i++)
                    _features[i] = _features[i + 1];
                
                if (_next != null)
                {
                    _features[99] = _next[0];
                    _next.RemoveAt(0);
                }
                else
                {
                    _features[max] = new SbnFeature();
                    NumFeatures--;
                }
            }
            else
            {
                _features[index] = new SbnFeature();
                NumFeatures--;
            }

            //var offset = index * 8;
            //var size = 800 - 8 - offset;
            //if (size > 0)
            //{
            //    //Buffer.BlockCopy(_buffer, offset + 8, _buffer, offset, size);
            //    if (Next != null)
            //    {
            //        this[99] = Next[0];
            //        Next.RemoveFeature(Next[0]);
            //    }
            //    else
            //    {
            //        Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, _buffer, NumFeatures * 8, 8);
            //        NumFeatures--;
            //    }
            //}
        }

        internal void RemoveFeature(SbnFeature feature)
        {
            var index = FindFeature(feature);
            if (index >= 0)
                RemoveAt(index);
        }

        /// <summary>
        /// Method to write a bin
        /// </summary>
        /// <param name="bid">The bin's id</param>
        /// <param name="sbnWriter">The writer to use</param>
        /// <param name="sbxWriter"></param>
        internal void Write(ref int bid, BinaryWriter sbnWriter, BinaryWriter sbxWriter = null)
        {
            if (sbxWriter != null)
            {
                BinaryIOExtensions.WriteBE(sbxWriter, (int)(sbnWriter.BaseStream.Position / 2));
                BinaryIOExtensions.WriteBE(sbxWriter, NumFeatures * 4);
            }
            BinaryIOExtensions.WriteBE(sbnWriter, bid++);
            BinaryIOExtensions.WriteBE(sbnWriter, NumFeatures*4);

            WriteBuffer(sbnWriter);
            if (Next != null)
                Next.Write(ref bid, sbnWriter, sbxWriter);
        }

        private int FindFeature(SbnFeature feature)
        {
            for (var i = 0; i < NumFeatures; i++)
                if (this[i].Equals(feature)) return i;
            return -1;
        }

        private void ReadBuffer(BinaryReader reader)
        {
            using (var msReader = new BinaryReader(new MemoryStream(reader.ReadBytes(NumFeatures*8))))
            {
                for (var i = 0; i < NumFeatures; i++)
                {
                    _features[i] = new SbnFeature(msReader);
                }
            }
            //reader.BaseStream.Read(_buffer, 0, NumFeatures*8);
            //for (var i = 0; i < NumFeatures; i++)
            //    Array.Reverse(_buffer, 4+8*i, 4);
        }

        private void WriteBuffer(BinaryWriter writer)
        {
            var size = NumFeatures*8;
            var buffer = new byte[size];
            using (var msWriter = new BinaryWriter(new MemoryStream(buffer)))
            {
                for (var i = 0; i < NumFeatures; i++)
                    _features[i].Write(msWriter);
                var ms = (MemoryStream)msWriter.BaseStream;
                writer.Write(ms.ToArray(), 0, size);
            }
            //Buffer.BlockCopy(_buffer, 0, buffer, 0, size);
            //for (var i = 0; i < NumFeatures; i++)
            //    Array.Reverse(buffer, 4 + 8 * i, 4);
            //writer.Write(buffer, 0, size);
        }
    }
}