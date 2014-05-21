using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using GeoAPI.DataStructures;
using GeoAPI.Geometries;

namespace SharpSbn
{
    /// <summary>
    /// A readonly implementation of an sbn index tree
    /// </summary>
    public class SbnQueryOnlyTree
    {
        private readonly Stream _sbnStream;
        private readonly Stream _sbxStream;
        
        private readonly SbnBinIndex _sbnBinIndex;

        private readonly SbnHeader _sbnHeader;
        private readonly object _indexLock = new object();

        private static int _defaultMaxCacheLevel;

        /// <summary>
        /// Method to open an sbn index file
        /// </summary>
        /// <param name="sbnFilename">The sbn index filename></param>
        /// <returns>An sbn index query structure</returns>
        public static SbnQueryOnlyTree Open(string sbnFilename)
        {
            if (string.IsNullOrEmpty(sbnFilename))
                throw new ArgumentNullException(sbnFilename);

            if (!File.Exists(sbnFilename))
                throw new FileNotFoundException("File not found", sbnFilename);

            var sbxFilename = Path.ChangeExtension(sbnFilename, "sbx");
            if (!File.Exists(sbxFilename))
                throw new FileNotFoundException("File not found", sbxFilename);

            var res = new SbnQueryOnlyTree(sbnFilename, sbxFilename);

            var sbxHeader = new SbnHeader();
            sbxHeader.Read(new BinaryReader(res._sbxStream));

            if (res._sbnHeader.NumRecords != sbxHeader.NumRecords)
                throw new SbnException("Sbn and Sbx do not serve the same number of features!");

            return res;
        }

        /// <summary>
        /// Static constructor for this class
        /// </summary>
        static SbnQueryOnlyTree()
        {
            DefaultMaxCacheLevel = 8;
        }

        /// <summary>
        /// Creates an istance of this class
        /// </summary>
        /// <param name="sbnFilename"></param>
        /// <param name="sbxFilename"></param>
        private SbnQueryOnlyTree(string sbnFilename, string sbxFilename)
        {
            _sbnStream = new FileStream(sbnFilename, FileMode.Open, FileAccess.Read, FileShare.Read, 8192);
            _sbnHeader = new SbnHeader();
            var sbnReader = new BinaryReader(_sbnStream);
            _sbnHeader.Read(sbnReader);
            _sbnBinIndex = SbnBinIndex.Read(sbnReader);

            _sbxStream = new FileStream(sbxFilename, FileMode.Open, FileAccess.Read, FileShare.Read, 8192);

            FirstLeafNodeId = (int)Math.Pow(2, GetNumberOfLevels(_sbnHeader.NumRecords) -1);
            MaxCacheLevel = DefaultMaxCacheLevel;

            SbnQueryOnlyNode.CreateRoot(this);
            _sbxStream.Position = 0;
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
        /// Method to query the feature's ids
        /// </summary>
        /// <param name="envelope">The extent in which to look for features</param>
        /// <returns>An enumeration of feature ids</returns>
        public IEnumerable<uint> QueryFids(Envelope envelope)
        {
            if (envelope == null || envelope.IsNull)
                return null;

            envelope = envelope.Intersection(_sbnHeader.Extent);
            var res = new List<uint>();
            if (envelope.IsNull) return res;

            byte minx, miny, maxx, maxy;
            ClampUtility.Clamp(_sbnHeader.Extent, envelope, out minx, out miny, out maxx, out maxy);

            var nodes = new List<SbnQueryOnlyNode>();
            Root.QueryNodes(minx, miny, maxx, maxy, nodes);
            nodes.Sort();
            foreach (var node in nodes)
            {
                node.QueryFids(minx, miny, maxx, maxy, res, false);
            }

            //Root.QueryFids(minx, miny, maxx, maxy, res);

            res.Sort();
            return res;
        }

        /// <summary>
        /// Gets a value indicating the root node for this tree
        /// </summary>
        private SbnQueryOnlyNode Root { get { return GetNode(1); } }

        /// <summary>
        /// Gets or sets a value indicating the default maximum level that is being cached
        /// </summary>
        internal int MaxCacheLevel { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating the default maximum level that is being cached
        /// </summary>
        /// <remarks>Must be greater or equal to <value>1</value></remarks>
        public static int DefaultMaxCacheLevel  
        {
            get { return _defaultMaxCacheLevel; }
            set
            {
                if (value < 1)
                    throw new ArgumentException("The default max cache level must be greater or equal 1");
                _defaultMaxCacheLevel = value;
            }
        }

        private SbnQueryOnlyNode GetNode(int id)
        {
            var level = (int)Math.Log(id, 2) + 1;
            if (level <= MaxCacheLevel)
            {
                return _sbnBinIndex.GetNode(id);
            }
            return null;
        }

        private byte[] GetBinData(int binIndex)
        {
            binIndex--;
            Monitor.Enter(_indexLock);
            _sbxStream.Seek(100 + binIndex*8, SeekOrigin.Begin);
            var sbxReader = new BinaryReader(_sbxStream);
            var sbnPosition = sbxReader.ReadUInt32BE()*2;
            var sbnSize = 8 + sbxReader.ReadInt32BE()*2;
            _sbnStream.Seek(sbnPosition, SeekOrigin.Begin);
            var res = new byte[sbnSize];
            _sbnStream.Read(res, 0, sbnSize);
            Monitor.Exit(_indexLock);
            return res;
        }

        /// <summary>
        /// Gets or sets a value indicating the first leaf node id
        /// </summary>
        internal int FirstLeafNodeId { get; private set; }

        /// <summary>
        /// Gets a value indicating the 2d extent of the tree
        /// </summary>
        public Envelope Extent { get { return _sbnHeader.Extent; } }

        /// <summary>
        /// Gets a value indicating the range of the z-ordinates
        /// </summary>
        public Interval ZRange { get { return _sbnHeader.ZRange; } }

        /// <summary>
        /// Gets a value indicating the range of the m-ordinates
        /// </summary>
        public Interval MRange { get { return _sbnHeader.MRange; } }


        private class SbnQueryOnlyNode : IComparable, IComparable<SbnQueryOnlyNode>
        {
            private readonly SbnQueryOnlyTree _tree;
            private readonly SbnFeature[] _features;
            private readonly byte _minX, _minY, _maxX, _maxY;

            internal int Nid { get; private set; }

            internal static void CreateRoot(SbnQueryOnlyTree tree)
            {
                var root = new SbnQueryOnlyNode(tree, 1, new byte[] {0, 0, 255, 255});
            }

            private SbnQueryOnlyNode(SbnQueryOnlyTree tree, int nid, byte[] splitBounds)
            {
                _tree = tree;
                Nid = nid;
                _minX = splitBounds[0];
                _minY = splitBounds[1];
                _maxX = splitBounds[2];
                _maxY = splitBounds[3];

                _features = ReadBins(nid, _tree._sbnBinIndex);
                if (Level <= _tree.MaxCacheLevel)
                    _tree._sbnBinIndex.CacheNode(this);
            }

            private SbnFeature[] ReadBins(int nid, SbnBinIndex binIndex)
            {
                var numFeatures = binIndex.GetNumFeatures(nid);
                var res = new SbnFeature[numFeatures];
                if (numFeatures == 0)
                {
                    return res;
                }

                var firstBinIndex = binIndex.GetFirstBinIndex(nid);
                var numBins = (int)Math.Ceiling(numFeatures / 100d);

                for (var i = 0; i < numBins; i++)
                {
                    using (var ms = new BinaryReader(new MemoryStream(_tree.GetBinData(firstBinIndex + i))))
                    {
                        var bin = new SbnBin();
                        var binId = bin.Read(ms);
                        if (binId != firstBinIndex + i)
                            throw new SbnException("Corrupt sbn file");
                        bin.CopyTo(res, i * 100);
                    }
                }
                return res;
            }

            internal void QueryFids(byte minx, byte miny, byte maxx, byte maxy, List<uint> fids, bool checkChildren)
            {
                if (ContainedBy(minx, miny, maxx, maxy))
                {
                    AddAllFidsInNode(fids, checkChildren);
                    return;
                }

                foreach (var feature in _features)
                {
                    if (feature.Intersects(minx, maxx, miny, maxy))
                        fids.Add(feature.Fid);
                }

                if (checkChildren && Nid < _tree.FirstLeafNodeId)
                {
                    var child = GetChild(0);
                    if (child.Intersects(minx, miny, maxx, maxy))
                        child.QueryFids(minx, miny, maxx, maxy, fids, true);

                    child = GetChild(1);
                    if (child.Intersects(minx, miny, maxx, maxy))
                        child.QueryFids(minx, miny, maxx, maxy, fids, true);
                }
            }

            private void AddAllFidsInNode(List<uint> list, bool checkChildren)
            {
                foreach (var sbnFeature in _features)
                {
                    list.Add(sbnFeature.Fid);
                }

                if (checkChildren && Nid < _tree.FirstLeafNodeId)
                {
                    GetChild(0).AddAllFidsInNode(list, true);
                    GetChild(1).AddAllFidsInNode(list, true);
                }
            }

            private SbnQueryOnlyNode GetChild(int childIndex)
            {
                var nodeIndex = Nid * 2 + childIndex;
                SbnQueryOnlyNode res = null;
                if (Level <= _tree.MaxCacheLevel)
                    res = _tree.GetNode(nodeIndex);
                if (res != null)
                    return res;

                res = new SbnQueryOnlyNode(_tree, nodeIndex, GetSplitBounds(childIndex));
                return res;

            }

            private byte[] GetSplitBounds(int childIndex)
            {
                var splitAxis = Level % 2;// == 1 ? 'x' : 'y';

                var mid = GetSplitOridnate(splitAxis);

                var res = new[] { _minX, _minY, _maxX, _maxY };
                switch (splitAxis)
                {
                    case 1: // x-ordinate
                        switch (childIndex)
                        {
                            case 0:
                                res[0] = (byte)(mid + 1);
                                break;
                            case 1:
                                res[2] = mid;
                                break;
                        }
                        break;
                    case 0: // y-ordinate
                        switch (childIndex)
                        {
                            case 0:
                                res[1] = (byte)(mid + 1);
                                break;
                            case 1:
                                res[3] = mid;
                                break;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("childIndex");
                }

                return res;
            }

            /// <summary>
            /// Compute the split ordinate for a given <paramref name="splitAxis"/>
            /// </summary>
            /// <param name="splitAxis">The axis</param>
            /// <returns>The ordinate</returns>
            private byte GetSplitOridnate(int splitAxis)
            {
                var mid = (splitAxis == 1)
                    ? /*(int)*/ (byte)((_minX + _maxX) / 2.0 + 1)
                    : /*(int)*/ (byte)((_minY + _maxY) / 2.0 + 1);

                return (byte)(mid - mid % 2);
            }
            /// <summary>
            /// Gets the node's level
            /// </summary>
            private int Level { get { return (int)Math.Log(Nid, 2) + 1; } }

            /// <summary>
            /// Intersection predicate function
            /// </summary>
            /// <param name="minX">lower x-ordinate</param>
            /// <param name="minY">lower y-ordinate</param>
            /// <param name="maxX">upper x-ordinate</param>
            /// <param name="maxY">upper y-ordinate</param>
            /// <returns><value>true</value> if this node's bounding box intersect with the bounding box defined by <paramref name="minX"/>, <paramref name="maxX"/>, <paramref name="minY"/> and <paramref name="maxY"/>, otherwise <value>false</value></returns>
            private bool Intersects(byte minX, byte minY, byte maxX, byte maxY)
            {
                return !(minX > _maxX || maxX < _minX || minY > _maxY || maxY < _minY);
            }

            /// <summary>
            /// ContainedBy predicate function
            /// </summary>
            /// <param name="minX">lower x-ordinate</param>
            /// <param name="minY">lower y-ordinate</param>
            /// <param name="maxX">upper x-ordinate</param>
            /// <param name="maxY">upper y-ordinate</param>
            /// <returns><value>true</value> if this node's bounding box contains the bounding box defined by <paramref name="minX"/>, <paramref name="maxX"/>, <paramref name="minY"/> and <paramref name="maxY"/>, otherwise <value>false</value></returns>
            private bool ContainedBy(byte minX, byte minY, byte maxX, byte maxY)
            {
                return _minX >= minX && _maxX <= maxX &&
                       _minY >= minY && _maxY <= maxY;
            }

            public void QueryNodes(byte minx, byte miny, byte maxx, byte maxy, List<SbnQueryOnlyNode> nodes)
            {

                if (!Intersects(minx, miny, maxx, maxy))
                    return;

                if (ContainedBy(minx, miny, maxx, maxy))
                {
                    AddAllNodes(nodes);
                    return;
                }

                // Add this node
                nodes.Add(this);

                // Test if child nodes are to be added
                if (Nid < _tree.FirstLeafNodeId)
                {
                    GetChild(0).QueryNodes(minx, miny, maxx, maxy, nodes);
                    GetChild(1).QueryNodes(minx, miny, maxx, maxy, nodes);
                }
            }

            private void AddAllNodes(List<SbnQueryOnlyNode> nodes)
            {
                nodes.Add(this);
                if (Nid < _tree.FirstLeafNodeId)
                {
                    GetChild(0).AddAllNodes(nodes);
                    GetChild(1).AddAllNodes(nodes);
                }
            }

            int IComparable.CompareTo(object obj)
            {
                if (obj == null)
                    throw new ArgumentNullException();
                if (!(obj is SbnQueryOnlyNode))
                    throw new ArgumentException("Object not a SbnQueryOnlyNode", "obj");

                return ((IComparable<SbnQueryOnlyNode>) this).CompareTo((SbnQueryOnlyNode) obj);
            }

            int IComparable<SbnQueryOnlyNode>.CompareTo(SbnQueryOnlyNode other)
            {
                if (other == null)
                    throw new ArgumentNullException("other");

                if (Nid < other.Nid) return -1;
                if (Nid > other.Nid) return 1;
                return 0;
            }
        }

        private class SbnBinIndex
        {
            private struct SbnNodeToBinIndexEntry
            {
                internal Int32 FirstBinIndex;
                internal Int32 NumFeatures;
                public SbnQueryOnlyNode Node
                { get; internal set; }


            }

            private readonly SbnNodeToBinIndexEntry[] _nodeToBin;

            internal static SbnBinIndex Read(BinaryReader reader)
            {
                if (reader.ReadInt32BE() != 1)
                    throw new SbnException("Sbn file corrupt");

                var length = reader.ReadInt32BE();
                var maxNodeId = length / 4;
                var nodeToBin = new SbnNodeToBinIndexEntry[maxNodeId + 1];
                for (var i = 1; i <= maxNodeId; i++)
                {
                    var binIndex = reader.ReadInt32BE();
                    var numFeatures = reader.ReadInt32BE();
                    if (binIndex > 0)
                        nodeToBin[i] = new SbnNodeToBinIndexEntry { FirstBinIndex = binIndex, NumFeatures = numFeatures };
                }

                return new SbnBinIndex(nodeToBin);
            }

            private SbnBinIndex(SbnNodeToBinIndexEntry[] nodeToBin)
            {
                _nodeToBin = nodeToBin;
            }

            internal Int32 GetFirstBinIndex(int nodeIndex)
            {
                if (nodeIndex < 1 || nodeIndex > _nodeToBin.GetUpperBound(0))
                    throw new ArgumentOutOfRangeException("nodeIndex");

                return _nodeToBin[nodeIndex].FirstBinIndex;
            }

            internal Int32 GetNumFeatures(int nodeIndex)
            {
                if (nodeIndex < 1 || nodeIndex > _nodeToBin.GetUpperBound(0))
                    return 0;

                return _nodeToBin[nodeIndex].NumFeatures;
            }

            public SbnQueryOnlyNode GetNode(int id)
            {
                if (id < _nodeToBin.Length)
                    return _nodeToBin[id].Node;
                return null;
            }

            public void CacheNode(SbnQueryOnlyNode sbnQueryOnlyNode)
            {
                if (sbnQueryOnlyNode.Nid < _nodeToBin.Length)
                    _nodeToBin[sbnQueryOnlyNode.Nid].Node = sbnQueryOnlyNode;
            }
        }

    }
}