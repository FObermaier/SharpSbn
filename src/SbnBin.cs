using System;
using System.Collections.Generic;
using System.IO;

namespace SbnSharp
{
    /// <summary>
    /// This is a utility class for reading and writing the index
    /// </summary>
    internal class SbnBin
    {
        internal const int MaxFeaturesPerBin = 8;

        public int Bid { get; private set; }

        private readonly List<SbnFeature> _features;
        public int NumFeatures { get; private set; }

        /// <summary>
        /// Creates a new bin with the provided <paramref name="bid"/>
        /// </summary>
        /// <param name="bid">The bin id</param>
        internal SbnBin(int bid)
        {
            Bid = bid;
            _features = new List<SbnFeature>(/*MaxFeaturesPerBin*/);
        }

        /// <summary>
        /// Creates a new bin while reading a stream with a binary reader
        /// </summary>
        /// <param name="reader">The reader</param>
        internal SbnBin(BinaryReader reader)
            :this(reader.ReadInt32BE())
        {
            NumFeatures = reader.ReadInt32BE();
            _features = new List<SbnFeature>(NumFeatures);
            //if (NumFeatures > MaxFeaturesPerBin)
            //    throw new SbnException("A bin can only hold " + MaxFeaturesPerBin + "items");
        }

        /// <summary>
        /// Method to load the bin's content
        /// </summary>
        /// <param name="reader">The reader</param>
        internal void ReadContent(BinaryReader reader)
        {
            var recordLength = reader.ReadInt32BE() * 2;
            var numFeatures = (recordLength / 8) ;

            //if (numFeatures != NumFeatures)
            //    throw new SbnException("Number of features don't match with bin's header definition");

            for (var i = 0; i < numFeatures; i++)
            {
                _features.Add(new SbnFeature(reader));
            }
        }

        /// <summary>
        /// Method to save the bin's header
        /// </summary>
        /// <param name="writer">The writer</param>
        internal void WriteHeader(BinaryWriter writer)
        {
            writer.WriteBE(Bid);
            writer.WriteBE(NumFeatures);
            var numFeatures = NumFeatures - 100;
            while (numFeatures > 0)
            {
                writer.WriteBE(-1);
                writer.WriteBE(0);
                numFeatures -= 100;
            }
        }

        /// <summary>
        /// Method to save the bin's content
        /// </summary>
        /// <param name="writer">The writer</param>
        internal void Write(BinaryWriter writer)
        {
            var block = 0;
            while (block * 100 < NumFeatures)
            {
                WriteBlock(writer, block);
                block += 1;
            }
        }

        private void WriteBlock(BinaryWriter writer, int block)
        {
            writer.WriteBE(Bid + block);
            var numFeatures = Math.Min(100, NumFeatures - block*100);
            writer.WriteBE(numFeatures * 2);
            for (int i = block * 100, j = 0; j < numFeatures; j++)
            {
                _features[i+j].Write(writer);
            }
        }

        /// <summary>
        /// Method to add a feature to a bin
        /// </summary>
        /// <param name="feature"></param>
        public void Add(SbnFeature feature)
        {
            //if (NumFeatures == MaxFeaturesPerBin)
            //    throw new SbnException("Bin is full");

            _features.Add(feature);
            NumFeatures ++;
        }

        /// <summary>
        /// Method to add a range of features
        /// </summary>
        /// <param name="features">The features</param>
        public void AddRange(IEnumerable<SbnFeature> features)
        {
            foreach (var feature in features)
                Add(feature);
        }

        public IList<SbnFeature> Features { get { return _features; } }
    }
}
