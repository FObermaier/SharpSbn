using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
//#if !(NET40 || NET45 || PCL)
//using FrameworkReplacemets;
//#if !NET35
//using FrameworkReplacemets.Linq;
//#endif
//using Enumerable = System.Linq.Enumerable;
//#else
//using System.Linq;
//#endif
#if !NET35
using Enumerable = FrameworkReplacements.Linq.Enumerable;
#else
using Enumerable = System.Linq.Enumerable;
#endif
#if !(NET40 || NET45 || PCL)
using FrameworkReplacements;
#endif
#if UseGeoAPI
using Interval = GeoAPI.DataStructures.Interval;
using GeoAPI.Geometries;
#else
using Interval = SharpSbn.DataStructures.Interval;
using Envelope = SharpSbn.DataStructures.Envelope;
#endif
using System.Threading;
using SbnEnumerable = FrameworkReplacements.Linq.Enumerable;

namespace SharpSbn
{
    /// <summary>
    /// A Sbn spatial tree
    /// </summary>
    public class SbnTree
    {
        /// <summary>
        /// Property to test if GeoAPI is used or not!
        /// </summary>
        public static bool HasGeoAPISupport
        {
            get
            {
                return
#if UseGeoAPI
                    true;
#else
                    false;
#endif
            }
        }

#if !PCL
        /// <summary>
        /// Method to describe the tree's content
        /// </summary>
        /// <param name="sbnTree"></param>
        /// <param name="writer"></param>
        public static void SbnToText(string sbnTree, TextWriter writer)
        {
            using (var br = new BinaryReader(File.OpenRead(sbnTree)))
            {
                // header
                var header = new SbnHeader();
                header.Read(br);
                writer.WriteLine(header.ToString());

                // Bin header
                writer.WriteLine("[BinHeader]");

                if (BinaryIOExtensions.ReadUInt32BE(br) != 1)
                    throw new SbnException("Invalid format, expecting 1");

                var maxNodeId = BinaryIOExtensions.ReadInt32BE(br) / 4;
                writer.WriteLine("#1, {0} => MaxNodeId = {1}", maxNodeId * 4, maxNodeId);

                var ms = new MemoryStream(br.ReadBytes(maxNodeId * 8));

                using (var msReader = new BinaryReader(ms))
                {
                    var index = 2;
                    while (msReader.BaseStream.Position < msReader.BaseStream.Length)
                    {
                        writer.WriteLine("#{2}, Index {0}, NumFeatures={1}",
                            BinaryIOExtensions.ReadInt32BE(msReader), BinaryIOExtensions.ReadInt32BE(msReader), index++);
                    }
                }

                writer.WriteLine("[Bins]");
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    var bin = new SbnBin();
                    var binId = bin.Read(br);
                    writer.Write("[SbnBin {0}: {1}]\n", binId, bin.NumFeatures);
                    for (var i = 0; i < bin.NumFeatures;i++)
                        writer.WriteLine("  "+ bin[i]);
                }

            }
            writer.Flush();
        }
#endif

#if !PCL
        
        /// <summary>
        /// Method to load an SBN index from a file
        /// </summary>
        /// <param name="sbnFilename">The filename</param>
        /// <returns>The SBN index</returns>
        public static SbnTree Load(string sbnFilename)
        {
            if (string.IsNullOrEmpty(sbnFilename))
                throw new ArgumentNullException("sbnFilename");

            if (!File.Exists(sbnFilename))
                throw new FileNotFoundException("File not found", sbnFilename);

            using (var stream = new FileStream(sbnFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Load(stream);
            }
        }
#endif

        /// <summary>
        /// Method to load an SBN index from a stream
        /// </summary>
        /// <param name="stream">The stream</param>
        /// <returns>The SBN index</returns>
        public static SbnTree Load(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            using (var reader = new BinaryReader(stream))
            {
                return new SbnTree(reader);
            }
        }

        private readonly SbnHeader _header = new SbnHeader();
        internal SbnNode[] Nodes;
#if NET40 || NET45
        private readonly HashSet<uint> _featureIds = new HashSet<uint>();
#else
        private readonly Dictionary<uint, uint> _featureIds = new Dictionary<uint, uint>();
#endif
        private readonly object _syncRoot = new object();

        /// <summary>
        /// Creates the tree reading data from the <paramref name="reader"/>
        /// </summary>
        /// <param name="reader">The reader to use</param>
        private SbnTree(BinaryReader reader)
        {
            Monitor.Enter(_syncRoot);

            _header = new SbnHeader();
            _header.Read(reader);

            BuildTree(_header.NumRecords);

            if (BinaryIOExtensions.ReadUInt32BE(reader) != 1)
                throw new SbnException("Invalid format, expecting 1");

            var maxNodeId = BinaryIOExtensions.ReadInt32BE(reader) / 4;
            var ms = new MemoryStream(reader.ReadBytes(maxNodeId * 8));
            using (var msReader = new BinaryReader(ms))
            {
                var indexNodeId = 1;
                while (msReader.BaseStream.Position < msReader.BaseStream.Length)
                {
                    var nid = BinaryIOExtensions.ReadInt32BE(msReader);
                    var featureCount = BinaryIOExtensions.ReadInt32BE(msReader);

                    if (nid > 1)
                    {
                        var node = Nodes[indexNodeId];
                        while (node.FeatureCount < featureCount)
                        {
                            var bin = new SbnBin();
                            bin.Read(reader);
                            node.AddBin(bin, true);
                        }
                        Debug.Assert(node.VerifyBins());
                    }
                    indexNodeId++;
                }
            }

            //Gather all feature ids
            GatherFids();

            //Assertions
            Debug.Assert(reader.BaseStream.Position == reader.BaseStream.Length);
            Debug.Assert(_featureIds.Count == _header.NumRecords);

            
            Monitor.Exit(_syncRoot);
        }

        public SbnTree(SbnHeader header)
        {
            _header = header;
            BuildTree(_header.NumRecords);
        }

        /// <summary>
        /// Gets a value Synchronization object
        /// </summary>
        public object SyncRoot { get { return _syncRoot; } }

        /// <summary>
        /// Get a value indicating if this tree is synchronized
        /// </summary>
        public bool IsSynchronized
        {
            get
            {
                if (!Monitor.TryEnter(_syncRoot))
                    return false;
                Monitor.Exit(_syncRoot);
                return true;
            }
        }

        /// <summary>
        /// Method to collect all feature ids in the tree
        /// </summary>
        private void GatherFids()
        {
            foreach (var sbnNode in Enumerable.Skip(Nodes, 1))
            {
                if (sbnNode == null) continue;

                foreach (var feature in sbnNode)
#if NET40 || NET45
                    _featureIds.Add(feature.Fid);
#else
                    _featureIds.Add(feature.Fid, 0);
#endif
            }
        }

        /// <summary>
        /// Method to build the tree
        /// </summary>
        /// <param name="numFeatures">The number of features in the tree</param>
        private void BuildTree(int numFeatures)
        {
            Built = false;
            NumLevels = GetNumberOfLevels(numFeatures);
            FirstLeafNodeId = (int)Math.Pow(2, NumLevels - 1);
            CreateNodes((int)Math.Pow(2, NumLevels));
        }

        /// <summary>
        /// Gets a value indicating the 2d extent of the tree
        /// </summary>
        public Envelope Extent { get { return _header.Extent; }}

        /// <summary>
        /// Gets a value indicating the range of the z-ordinates
        /// </summary>
        public Interval ZRange { get { return _header.ZRange; }}

        /// <summary>
        /// Gets a value indicating the range of the m-ordinates
        /// </summary>
        public Interval MRange { get { return _header.MRange; } }

        /// <summary>
        /// Event raised when a rebuild of the tree is required, 
        /// that requires access to all feature information.
        /// </summary>
        public event EventHandler<SbnTreeRebuildRequiredEventArgs> RebuildRequried;

        /// <summary>
        /// Event invoker for the <see cref="RebuildRequried"/> event
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnRebuildRequired(SbnTreeRebuildRequiredEventArgs e)
        {
            var handler = RebuildRequried;
            if (handler != null)
                handler(this, e);
        }

#if UseGeoAPI
        /// <summary>
        /// Method to insert a new feature to the tree
        /// </summary>
        /// <param name="fid">The feature's id</param>
        /// <param name="geometry">The feature's geometry</param>
        [CLSCompliant(false)]
        public void Insert(uint fid, GeoAPI.Geometries.IGeometry geometry)
        {
            Interval x, y, z, m;
            GeometryMetricExtensions.GetMetric(geometry, out x, out y, out z, out m);
            Insert(fid, geometry.EnvelopeInternal, z, m);

        }

        /// <summary>
        /// Method to create an <see cref="SbnFeature"/> from an id and a geometry
        /// </summary>
        /// <param name="fid">The feature's id</param>
        /// <param name="geometry">The geometry</param>
        /// <returns>A sbnfeature</returns>
        private SbnFeature ToSbnFeature(uint fid, GeoAPI.Geometries.IGeometry geometry)
        {
            return new SbnFeature(_header.Extent, fid, geometry.EnvelopeInternal);
        }

#endif

        /// <summary>
        /// Method to insert a new feature to the tree
        /// </summary>
        /// <param name="fid">The feature's id</param>
        /// <param name="envelope">The feature's geometry</param>
        [CLSCompliant(false)]
        public void Insert(uint fid, Envelope envelope, Interval? zRange = null, Interval? mRange = null)
        {
            // lock the tree
            Monitor.Enter(_syncRoot);

            // Convert to an sbnfeature
            var sbnFeature = ToSbnFeature(fid, envelope);

            var inserted = false;
            // Has the tree already been built?
            if (Built)
            {
                // Does the feature fit into the current tree, signal that 
                // the tree needs to be recreated in order to function properly.
                if (!_header.Extent.Contains(envelope))
                {
                    OnRebuildRequired(new SbnTreeRebuildRequiredEventArgs(fid, envelope, zRange, mRange));
                    Monitor.Exit(_syncRoot);
                    return;
                }

                // Compute number of features in tree
                var featureCount = FeatureCount + 1;

                // Does the new number of features require more levels?
                if (GetNumberOfLevels(featureCount) != NumLevels)
                {
                    // This can be done inplace.
                    RebuildTree(featureCount, sbnFeature);
                    inserted = true;
                }
            }
            
            //Insert the feature
            if (!inserted) Insert(sbnFeature);
            
            // Update the header metrics
            _header.AddFeature(fid, envelope, zRange ?? Interval.Create(), mRange ?? Interval.Create());

            // unlock the tree
            Monitor.Exit(_syncRoot);

        }

        /// <summary>
        /// Method to -inplace- rebuild the tree
        /// </summary>
        /// <param name="featureCount">The number of features for the tree</param>
        /// <param name="newFeature">The new feature to add</param>
        private void RebuildTree(int featureCount, SbnFeature newFeature)
        {
            var nodes = Nodes;
            _featureIds.Clear();

            BuildTree(featureCount);

            for (var i = 1; i < nodes.Length; i++)
            {
                foreach (var feature in nodes[i])
                    Insert(feature);
            }
            Insert(newFeature);

            CompactSeamFeatures();
        }

        /// <summary>
        /// Method to remove the feature <paramref name="fid"/>
        /// </summary>
        /// <param name="fid">The id of the feature</param>
        /// <param name="envelope">The envelope in which to search for the feature</param>
        [CLSCompliant(false)]
        public void Remove(uint fid, Envelope envelope = null)
        {
            Monitor.Enter(_syncRoot);

            envelope = envelope ?? _header.Extent;
            var searchFeature = new SbnFeature(_header, fid, envelope);
            Root.Remove(searchFeature);

            Monitor.Exit(_syncRoot);
        }

        /// <summary>
        /// Method to insert a feature to the tree
        /// </summary>
        /// <param name="feature"></param>
        internal void Insert(SbnFeature feature)
        {
            // Insert a feature into the tree
            Root.Insert(feature);
#if (NET40 || NET45)
            _featureIds.Add(feature.Fid);
#else
            _featureIds.Add(feature.Fid, 0);
#endif
        }

        /// <summary>
        /// Gets a value indicating that that the tree has been built.
        /// </summary>
        internal bool Built { get; set; }

        /// <summary>
        /// Gets a value indicating the number of levels in this tree
        /// </summary>
        public int NumLevels { get; private set; }

        /// <summary>
        /// Get a value indicating the number of features in the index
        /// </summary>
        public int FeatureCount { get { return Root.CountAllFeatures(); } }

        /// <summary>
        /// Gets a value indicating the id of the first leaf
        /// </summary>
        internal int FirstLeafNodeId { get; private set; }

        /// <summary>
        /// Gets the id of the last leaf node
        /// </summary>
        internal int LastLeafNodeId { get { return FirstLeafNodeId * 2 - 1; } }

        /// <summary>
        /// Method to create the nodes for this tree
        /// </summary>
        /// <param name="numNodes">The number of nodes</param>
        private void CreateNodes(int numNodes)
        {
            Nodes = new SbnNode[numNodes];
            Nodes[1] = new SbnNode(this, 1, 0, 0, 255, 255);
            Nodes[1].AddChildren();
        }

        /// <summary>
        /// Function to compute the number of levels required for the number of features to add
        /// </summary>
        /// <param name="featureCount">The number of features a tree should take</param>
        /// <returns>The number of levels</returns>
        private static int GetNumberOfLevels(int featureCount)
        {
            var levels = (int)Math.Log(((featureCount - 1) / 8.0 + 1), 2) + 1;
            if (levels < 2) levels = 2;
            if (levels > 24) levels = 24;

            return levels;
        }

        /// <summary>
        /// The root node
        /// </summary>
        internal SbnNode Root { get { return Nodes[1]; } }

#if !PCL
        /// <summary>
        /// Method to save the tree to a file
        /// </summary>
        /// <param name="sbnName">The filename</param>
        public void Save(string sbnName)
        {
            Monitor.Enter(_syncRoot);

            if (string.IsNullOrEmpty(sbnName))
                throw new ArgumentNullException("sbnName");

            var sbxName = Path.ChangeExtension(sbnName, "sbx");

            if (File.Exists(sbnName)) File.Delete(sbnName);
            if (File.Exists(sbxName)) File.Delete(sbxName);

            var sbnStream = new FileStream(sbnName, FileMode.Create, FileAccess.Write, FileShare.None);
            var sbxStream = new FileStream(sbxName, FileMode.Create, FileAccess.Write, FileShare.None);
            using (var sbnWriter = new BinaryWriter(sbnStream))
            using (var sbxWriter = new BinaryWriter(sbxStream))
                Write(sbnWriter, sbxWriter);

            Monitor.Exit(_syncRoot);
        }
#endif

        /// <summary>
        /// Method to get header values for the shapefile header record
        /// </summary>
        /// <param name="numBins">The number of bins</param>
        /// <param name="lastBinIndex">The index of the last bin that contains features</param>
        private void GetHeaderValues(out int numBins, out int lastBinIndex)
        {
            numBins = 0;
            lastBinIndex = 0;
            for (var i = 1; i < Nodes.Length; i++)
            {
                if (Nodes[i].FeatureCount > 0)
                {
                    var numBinsForNode = (int)Math.Ceiling(Nodes[i].FeatureCount/100d);
                    lastBinIndex = i;
                    numBins += numBinsForNode;
                }
            }
        }

        /// <summary>
        /// Method to write the tree
        /// </summary>
        /// <param name="sbnsw">A writer for the sbn stream</param>
        /// <param name="sbxsw">A writer for the sbx stream</param>
        private void Write(BinaryWriter sbnsw, BinaryWriter sbxsw)
        {
            // Gather header data
            int numBins, lastBinIndex;
            GetHeaderValues(out numBins, out lastBinIndex);

            // we have one additional bin
            numBins++;

            // first bin descriptors
            var numBinHeaderRecords = lastBinIndex;
            var binHeaderSize = (numBinHeaderRecords) * 8;

            // then bins with features
            var usedBinSize = numBins * 8;

            var sbxSize = 100 + usedBinSize;
            var sbnSize = 100 + binHeaderSize + usedBinSize + FeatureCount*8;

            // Write headers
            _header.Write(sbnsw, sbnSize);
            _header.Write(sbxsw, sbxSize);

            // sbn and sbx records
            // first create bin descriptors record
            var recLen = (numBinHeaderRecords) * 4;
            BinaryIOExtensions.WriteBE(sbnsw, 1);
            BinaryIOExtensions.WriteBE(sbnsw, recLen);

            BinaryIOExtensions.WriteBE(sbxsw, 50);
            BinaryIOExtensions.WriteBE(sbxsw, recLen);

            WriteBinHeader(sbnsw, lastBinIndex);

            WriteBins(sbnsw, sbxsw);
        }

        /// <summary>
        /// Method to write the bin header to the sbn file
        /// </summary>
        /// <param name="sbnsw"></param>
        /// <param name="lastBinIndex"></param>
        private void WriteBinHeader(BinaryWriter sbnsw, int lastBinIndex)
        {
            var binIndex = 2;
            for (var i = 1; i <= lastBinIndex; i++)
            {
                if (Nodes[i].FeatureCount > 0)
                {
                    BinaryIOExtensions.WriteBE(sbnsw, binIndex);
                    BinaryIOExtensions.WriteBE(sbnsw, Nodes[i].FeatureCount);
                    binIndex += (int) Math.Ceiling(Nodes[i].FeatureCount/100d);
                }
                else
                {
                    BinaryIOExtensions.WriteBE(sbnsw, -1);
                    BinaryIOExtensions.WriteBE(sbnsw, 0);
                }
            }
        }

        /// <summary>
        /// Method to write the bins
        /// </summary>
        /// <param name="sbnWriter">The writer for the sbn file</param>
        /// <param name="sbxWriter">The writer for the sbx file</param>
        private void WriteBins(BinaryWriter sbnWriter, BinaryWriter sbxWriter)
        {
            var binid = 2;
            for (var i = 1; i < Nodes.Length; i++)
            {
                if (Nodes[i].FirstBin != null)
                    Nodes[i].FirstBin.Write(ref binid, sbnWriter, sbxWriter); 
            }

            /*
            using (var binIt = new SbnBinEnumerator(this)) 
            while (binIt.MoveNext())
            {
                binIt.Current.Write(ref binid, sbnWriter, sbxWriter);
            }*/
        }

        public bool VerifyNodes()
        {
#if DEBUG
            foreach (var node in Enumerable.Skip(Nodes, 1))
            {
                if (!node.VerifyBins())
                    return false;
            }
#endif
            return true;
        }


        //private class SbnBinEnumerator : IEnumerator<SbnBin>
        //{
        //    private readonly SbnTree _tree;
        //    private SbnNode _currentNode;
        //    private SbnBin _currentBin, _lastBin;
        //    private bool _finished;

        //    public SbnBinEnumerator(SbnTree tree)
        //    {
        //        _tree = tree;
        //    }

        //    public void Dispose()
        //    {
        //    }

        //    public bool MoveNext()
        //    {
        //        if (_finished)
        //            return false;

        //        _lastBin = _currentBin;
        //        var res = SeekNextBin(1);
        //        Debug.Assert(!ReferenceEquals(_lastBin, _currentBin));
        //        return res;
        //    }

        //    bool SeekNextBin(int depth)
        //    {
        //        Debug.Assert(depth < 1000);

        //        if (_currentNode == null)
        //        {
        //            _currentNode = _tree.Nodes[1];
        //            if (_currentNode.FirstBin == null)
        //                return SeekNextBin(depth+1);
        //        }

        //        if (_currentBin == null)
        //        {
        //            _currentBin = _currentNode.FirstBin;
        //            if (_currentBin == null)
        //            {
        //                if (_currentNode.Nid == _tree.LastLeafNodeId)
        //                {
        //                    _finished = true;
        //                    return false;
        //                }
        //                _currentNode = _tree.Nodes[_currentNode.Nid + 1];
        //                return SeekNextBin(depth + 1);
        //            }
        //            return true;


        //        }

        //        _currentBin = _currentBin.Next;
        //        if (_currentBin == null)
        //        {
        //            if (_currentNode.Nid == _tree.LastLeafNodeId)
        //            {
        //                _finished = true;
        //                return false;
        //            }

        //            _currentNode = _tree.Nodes[_currentNode.Nid + 1];
        //            return SeekNextBin(depth + 1);
        //        }

        //        return true;
        //    }

        //    public void Reset()
        //    {
        //        _currentNode = null;
                
        //        _currentBin = null;
        //        _finished = false;
        //    }

        //    public SbnBin Current { get { return _currentBin; } }

        //    object IEnumerator.Current
        //    {
        //        get { return Current; }
        //    }
        //}

        /// <summary>
        /// Method to compute the number of features in a given level
        /// </summary>
        /// <param name="level">The level</param>
        /// <returns>The number of features</returns>
        public int FeaturesInLevel(int level)
        {
            // return the number of features in a level
            var start = (int)Math.Pow(2, level - 1);
            var end = 2 * start - 1;
            var featureCount = 0;
            foreach (var n in SbnEnumerable.GetRange(Nodes, start, end - start + 1))
                featureCount += n.FeatureCount;
            return featureCount;
        }

        /// <summary>
        /// Method to describe the tree
        /// </summary>
        /// <param name="out">The textwriter to use</param>
        public void DescribeTree(TextWriter @out)
        {
#if VERBOSE
            if (@out == null)
                throw new ArgumentNullException("out");

            @out.WriteLine("#Description");
            @out.WriteLine("#            f=full [0, 1]");
            @out.WriteLine("#                sf=features on seam");
            @out.WriteLine("#                   h=holdfeatures");
            @out.WriteLine("#level node  f   sf h");
            for (var i = 1; i <= NumLevels; i++)
            {
                var nodes = GetNodesOfLevel(i);
                foreach (var node in nodes)
                {
                    @out.WriteLine("{0,5} {1,5}", i, node.ToStringVerbose());
                }
            }
#else
            //We are not verbose so we don't do anything
#endif
        }

        /// <summary>
        /// Method to create an <see cref="SbnFeature"/> from an id and an envelope
        /// </summary>
        /// <param name="fid">The feature's id</param>
        /// <param name="envelope">The geometry</param>
        /// <returns>A sbnfeature</returns>
        private SbnFeature ToSbnFeature(uint fid, Envelope envelope)
        {
            return new SbnFeature(_header.Extent, fid, envelope);
        }

        /// <summary>
        /// Method to query the ids of features that intersect with <paramref name="extent"/>
        /// </summary>
        /// <param name="extent">The extent</param>
        /// <returns>An enumeration of feature ids</returns>
        public IEnumerable<uint> QueryFids(Envelope extent)
        {
            var res = new List<uint>();

            Monitor.Enter(_syncRoot);

            extent = _header.Extent.Intersection(extent);
            byte minx, miny, maxx, maxy;
            ClampUtility.Clamp(_header.Extent, extent,
                               out minx, out miny, out maxx, out maxy);

            Root.QueryFids(minx, miny, maxx, maxy, res);

            Monitor.Exit(_syncRoot);

            res.Sort();

            return res;
        }

        /// <summary>
        /// Method to get the nodes of a specic level
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public IList<SbnNode> GetNodesOfLevel(int level)
        {
            if (level < 1 || level > NumLevels)
                throw new ArgumentOutOfRangeException("level");

            var start = (int)Math.Pow(2, level - 1);
            var end = 2 * start - 1;
            return NumPySlicing.GetRange(Nodes, start, end, 1);
        }

        /// <summary>
        /// Method to create an <see cref="SbnTree"/> from a collection of (id, geometry) tuples
        /// </summary>
        /// <param name="boxedFeatures">The (id, geometry) tuples</param>
        /// <returns>The newly created tree</returns>
        public static SbnTree Create(ICollection<Tuple<uint, Envelope>> boxedFeatures, Interval? zRange = null, Interval? mRange = null)
        {
            Interval x, y, z, m;
            GetIntervals(boxedFeatures, out x, out y, out z, out m);
            if (zRange.HasValue) z = z.ExpandedByInterval(zRange.Value);
            if (mRange.HasValue) m = m.ExpandedByInterval(mRange.Value);

            var tree = new SbnTree(new SbnHeader(boxedFeatures.Count, x, y, z, m));
            foreach (var boxedFeature in boxedFeatures)
            {
                tree.Insert(tree.ToSbnFeature(boxedFeature.Item1, boxedFeature.Item2));
            }
            
            tree.CompactSeamFeatures();
            return tree;
        }

        /// <summary>
        /// Method to get some of the shapefile header values.
        /// </summary>
        /// <param name="geoms">An enumeration of (id, geometry) tuples</param>
        /// <param name="xrange">The x-extent</param>
        /// <param name="yrange">The y-extent</param>
        /// <param name="zrange">The z-extent</param>
        /// <param name="mrange">The m-extent</param>
        private static void GetIntervals(IEnumerable<Tuple<uint, Envelope>> geoms, out Interval xrange, out Interval yrange,
            out Interval zrange, out Interval mrange)
        {
            xrange = Interval.Create();
            yrange = Interval.Create();
            zrange = Interval.Create();
            mrange = Interval.Create();

            foreach (var tuple in geoms)
            {
                Interval x2Range, y2Range, z2Range, m2Range;
                GeometryMetricExtensions.GetMetric(tuple.Item2, out x2Range, out y2Range, out z2Range, out m2Range);
                xrange = xrange.ExpandedByInterval(x2Range);
                yrange = yrange.ExpandedByInterval(y2Range);
                zrange = zrange.ExpandedByInterval(z2Range);
                mrange = mrange.ExpandedByInterval(m2Range);
            }
        }

#if UseGeoAPI
        /// <summary>
        /// Method to create an <see cref="SbnTree"/> from a collection of (id, geometry) tuples
        /// </summary>
        /// <param name="boxedFeatures">The (id, geometry) tuples</param>
        /// <returns>The newly created tree</returns>
        public static SbnTree Create(ICollection<Tuple<uint, IGeometry>> boxedFeatures, Interval? zRange = null, Interval? mRange = null)
        {
            Interval x, y, z, m;
            GetIntervals(boxedFeatures, out x, out y, out z, out m);
            if (zRange.HasValue) z = z.ExpandedByInterval(zRange.Value);
            if (mRange.HasValue) m = m.ExpandedByInterval(mRange.Value);

            var tree = new SbnTree(new SbnHeader(boxedFeatures.Count, x, y, z, m));
            foreach (var boxedFeature in boxedFeatures)
            {
                tree.Insert(tree.ToSbnFeature(boxedFeature.Item1, boxedFeature.Item2));
            }
            
            tree.CompactSeamFeatures();
            return tree;
        }

        /// <summary>
        /// Method to get some of the shapefile header values.
        /// </summary>
        /// <param name="geoms">An enumeration of (id, geometry) tuples</param>
        /// <param name="xrange">The x-extent</param>
        /// <param name="yrange">The y-extent</param>
        /// <param name="zrange">The z-extent</param>
        /// <param name="mrange">The m-extent</param>
        private static void GetIntervals(IEnumerable<Tuple<uint, IGeometry>> geoms, out Interval xrange, out Interval yrange,
            out Interval zrange, out Interval mrange)
        {
            xrange = Interval.Create();
            yrange = Interval.Create();
            zrange = Interval.Create();
            mrange = Interval.Create();

            foreach (var tuple in geoms)
            {
                Interval x2Range, y2Range, z2Range, m2Range;
                GeometryMetricExtensions.GetMetric(tuple.Item2, out x2Range, out y2Range, out z2Range, out m2Range);
                xrange = xrange.ExpandedByInterval(x2Range);
                yrange = yrange.ExpandedByInterval(y2Range);
                zrange = zrange.ExpandedByInterval(z2Range);
                mrange = mrange.ExpandedByInterval(m2Range);
            }
        }
#endif
        /// <summary>
        /// Method to compact this <see cref="SbnTree"/>.
        /// </summary>
        private void CompactSeamFeatures()
        {
            // the mystery algorithm - compaction? optimization? obfuscation?
            if (NumLevels < 4)
                return;

            var start = FirstLeafNodeId/2 - 1;
            if (start < 3) start = 3;

            var end = start / 8;
            if (end < 1) end = 1;

            foreach (var node in NumPySlicing.GetRange(Nodes, start, end, -1))
            {
                var id = node.Nid;
                var children = SbnEnumerable.GetRange(Nodes, id * 2, 2);
                foreach (var child in children)
                {
                    // There are no items to pull up
                    if (child.FeatureCount == 0) continue;

                    var cid = child.Nid;
                    var grandchildren = SbnEnumerable.GetRange(Nodes, cid * 2, 2);
                    var gccount = 0;
                    foreach (var gcnode in grandchildren)
                        gccount += gcnode.FeatureCount;

                    //Debug.WriteLine("Node {0} has {1} GC", id, gccount);
                    if (gccount == 0)
                    {
                        //Debug.WriteLine("Slurping {0} features from node {1}", child.AllFeatures().Count, child.id);
                        //node.features.AddRange(child.features);

                        // this is weird but it works
                        if (child.FeatureCount < 4)
                        {
                            if (node.FirstBin == null)
                                node.FirstBin = new SbnBin();

                            //for (var i = 0; i < child.FeatureCount; i++)
                            //{
                            //    //node.LastBin.AddFeature(child.FirstBin[i]);
                            //    node.LastBin.AddFeature(child.RemoveAt(0));
                            //}

                            while (child.FeatureCount > 0)
                            { node.LastBin.AddFeature(child.RemoveAt(0)); }
                            //Debug.Assert(child.FeatureCount == 0);
                            //child.FirstBin = null;
                        }
                    }
                }
            }
            Built = true;
        }
    }
}