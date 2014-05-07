using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GeoAPI.Geometries;

namespace SbnSharp
{
    public class OldSbnTree
    {
        /// <summary>
        /// Method to create a tree from a collection of
        /// </summary>
        /// <param name="idExtents"></param>
        /// <returns></returns>
        public static OldSbnTree Create(ICollection<Tuple<uint, Envelope>> idExtents)
        {
            var extent = GetExtents(idExtents);
            var sbnTree = new OldSbnTree(idExtents.Count, extent);
            foreach (var idExtent in idExtents)
            {
                sbnTree.Insert(CreateFeature(extent, idExtent));
            }

            sbnTree.CompactSeamFeatures();
            return sbnTree;
        }

        private static SbnFeature CreateFeature(Envelope sfExtent, Tuple<uint, Envelope> idExtent)
        {
            return new SbnFeature(sfExtent, idExtent.Item1, idExtent.Item2);
        }

        private static Envelope GetExtents(IEnumerable<Tuple<uint, Envelope>> idExtents)
        {
            var res = new Envelope();
            foreach (var idExtent in idExtents)
                res.ExpandToInclude(idExtent.Item2);
            return res;
        }

        private SbnHeader _header = new SbnHeader();
        
        //https://github.com/drwelby/hasbeen/blob/master/bextree.py
        internal List<OldSbnNode> Nodes { get; private set; }
        private int _levels;
        private readonly HashSet<uint> _featureIds = new HashSet<uint>();

        internal int FirstLeafId { get; private set; }
        
        /// <summary>
        /// Gets a value indicating the root node
        /// </summary>
        public OldSbnNode Root { get; private set; }

        public int Depth { get { return _levels; } }

        /// <summary>
        /// Method to get the nodes of a specic level
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public List<OldSbnNode> GetNodesOfLevel(int level)
        {
            if (level < 1 || level > _levels)
                throw new ArgumentOutOfRangeException("level");

            var start = (int)Math.Pow(2, level - 1);
            var end = 2 * start - 1;
            return Nodes.GetRange(start, end - start + 1);
        }

        /// <summary>
        /// Method to describe the tree
        /// </summary>
        /// <param name="out">The textwriter to use</param>
        public void DescribeTree(TextWriter @out)
        {
#if VERBOSE
            var sfh = new List<OldSbnNode>();
            if (@out == null)
                throw new ArgumentNullException("out");

            @out.WriteLine("#Description");
            @out.WriteLine("#            f=full [0, 1]");
            @out.WriteLine("#                sf=features on seam");
            @out.WriteLine("#                   h=holdfeatures");
            @out.WriteLine("#level node  f   sf h");
            for (var i = 1; i <= Depth; i++)
            {
                var nodes = GetNodesOfLevel(i);
                foreach (var node in nodes)
                {
                    @out.WriteLine("{0,5} {1,5}", i, node.ToStringVerbose() );
                    if (node.features.Count > 0 && node.holdfeatures.Count > 0)
                    {
                        sfh.Add(node);
                    }
                }
            }
#else
            throw new NotSupportedException();
#endif
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="featureCount">The initial number of features</param>
        /// <param name="extent">The extent of the tree</param>
        public OldSbnTree(int featureCount, Envelope extent)
        {
            BuildTree(featureCount, extent);
        }

        /// <summary>
        /// Method to build the tree for <paramref name="featureCount"/> features and <paramref name="extent"/> bounding box.
        /// </summary>
        /// <param name="featureCount">The number of features to add</param>
        /// <param name="extent">The area covered by the index</param>
        private void BuildTree(int featureCount, Envelope extent)
        {
            _header = new SbnHeader(featureCount, extent);
            
            Nodes = new List<OldSbnNode>();
            _levels = GetNumberOfLevels(featureCount);
            FirstLeafId = (int)Math.Pow(2, _levels - 1);

            for (var i = 0; i < (uint) Math.Pow(2, _levels); i++)
            {
                var n = new OldSbnNode(this, i);
                Nodes.Add(n);
            }
            Root = Nodes[1];
            //Root.id = 1;
            //Root.tree = this;
            //Root.split = 'x';
            Root.xmin = 0;
            Root.xmax = 255;
            Root.ymin = 0;
            Root.ymax = 255;
            Root.AddSplitCoord();
            Root.Grow();
        }

        private static int GetNumberOfLevels(int featureCount)
        {
            var levels = (int)Math.Log(((featureCount - 1) / 8.0 + 1), 2) + 1;
            if (levels < 2) levels = 2;
            if (levels > 15) levels = 15;

            return levels;
        }

        /// <summary>
        /// Method to insert a feature to the tree
        /// </summary>
        /// <param name="feature"></param>
        internal void Insert(SbnFeature feature)
        {
            // Insert a feature into the tree
            Root.Insert(feature);
            _featureIds.Add(feature.Fid);
        }

        internal List<OldSbnBin> ToBins()
        {
            var bins = new List<OldSbnBin>();
            var bid = 0;
            var b = new OldSbnBin(bid++);
            bins.Add(b);
            foreach (var node in Nodes)
            {
                b = new OldSbnBin(bid++);
                b.AddRange(node.AllFeatures());
                //b.Features = node.AllFeatures();
                bins.Add(b);
            }
            return bins;
        }

        public int FeaturesInLevel(int level)
        {
            // return the number of features in a level
            var start = (int) Math.Pow(2, level - 1);
            var end = 2*start - 1;
            var featureCount = 0;
            foreach (var n in Nodes.GetRange(start, end - start + 1))
                featureCount += n.features.Count;
            return featureCount;
        }

        private void CompactSeamFeatures()
        {
            // the mystery algorithm - compaction? optimization? obfuscation?
            if (_levels < 4)
                return;

            if (_levels > 4)
            {
                var start = FirstLeafId/2 - 1;
                var end = start/8;
                if (start < 3) start = 3;
                if (end < 1) end = 1;

                foreach(var node in Nodes.GetRange(start, end, -1))
                {
                    var id = node.id;
                    var children = Nodes.GetRange(id*2, 2);
                    foreach (var child in children)
                    {
                        // There are no items to pull up
                        if (child.Count == 0) continue;

                        var cid = child.id;
                        var grandchildren = Nodes.GetRange(cid*2, 2);
                        var gccount = 0;
                        foreach (var gcnode in grandchildren)
                            gccount += gcnode.AllFeatures().Count;

                        Debug.WriteLine("Node {0} has {1} GC", id,gccount);
                        if (gccount == 0)
                        {
                            //Debug.WriteLine("Slurping {0} features from node {1}", child.AllFeatures().Count, child.id);
                            //node.features.AddRange(child.features);
                            
                            // this is weird but it works
                            if (child.AllFeatures().Count < 4)
                            {
                                node.features.AddRange(child.AllFeatures());
                                child.features.Clear();
                                child.holdfeatures.Clear();
                            }
                        }
                    }
                }
            }
        }

        //private void CompactSeamFeatures2()
        //{
        //    // another run at the mystery algorithm

        //    //for node in self.nodes[1:self.firstleafid/4]:
        //    var start = FirstLeafId/2 - 1;
        //    var end = start/8;
        //    if (start < 3) start = 3;
        //    if (end < 1) end = 1;
            
        //    //for node in self.nodes[start:end:-1]:
        //    for (var i = end; i > start; i--)
        //    {
        //        var node = Nodes[(int) i];

        //        //if len(node.features) > 0 and self.levels < 6:
        //        //   continue
        //        var id = node.id;
        //        var children = Nodes.GetRange((int) id*2, 2);
        //        var grandchildren = Nodes.GetRange((int) id*4, 4);
        //        var gccount = 0;
        //        foreach (var gcnode in grandchildren)
        //            gccount += gcnode.AllFeatures().Count;
        //        //print "Node %s has %s GC" % (id,gccount)
        //        if (gccount == 0)
        //        {
        //            foreach (var cnode in children)
        //            {
        //                if (cnode.AllFeatures().Count + node.features.Count > 8)
        //                    continue;
        //                //print "Slurping %s features from node %s" % (len(cnode.features),cnode.id)
        //                node.features.AddRange(cnode.AllFeatures());
        //                //node.features.extend(cnode.features)
        //                cnode.features.Clear();
        //                cnode.holdfeatures.Clear();
        //            }
        //        }
        //    }
        //    // compact unsplit nodes see cities/248
        //    return;

        //    //for node in self.nodes[start:end:-1]:
        //    for (var i = end; i > start; i--)
        //    {
        //        var node = Nodes[(int) i];
        //        var level = Math.Ceiling(Math.Log(node.id, 2));
        //        var id = node.id;
        //        var children = Nodes.GetRange((int) id*2 - 1, 2);
        //        children.Reverse();
        //        var empty = false;
        //        var childrenfeatures = 0;
        //        foreach (var child in children)
        //        {
        //            //if not child.full:
        //            //    held = True
        //            var cid = (int) child.id;
        //            childrenfeatures += child.features.Count;
        //            var grandchildren = Nodes.GetRange(cid*2, 2);
        //            foreach (var gcnode in grandchildren)
        //            {
        //                if (gcnode.features.Count == 0)
        //                    empty = true;
        //            }
        //        }
        //        //print "Node %s childless: %s" % (cid,empty)
        //        Debug.WriteLine("{0} : {1}", empty, childrenfeatures);
        //        if (empty && childrenfeatures > 0)
        //        {
        //            //node.features.extend(child.features)
        //            foreach (var child in children)
        //            {
        //                if (child.SiblingFeatureCount() < 4 &&
        //                    child.SiblingFeatureCount() > 0)
        //                    continue;
        //                //if self.featuresinlevel(level) >= 8:
        //                //    return
        //                //print "Slurping %s features from node %s" % (len(child.allfeatures()),child.id)
        //                node.features.AddRange(child.AllFeatures());
        //                //node.full = True
        //                child.features.Clear();
        //                child.holdfeatures.Clear();
        //            }
        //        }
        //        return;
        //    }
        //}

        public void AddFeature(uint fid, IGeometry geometry)
        {
            if (_featureIds.Contains(fid))
                throw new InvalidOperationException("A feature with this id is already present");

            var extent = geometry.EnvelopeInternal;
            if (!_header.Envelope.Contains(extent) || GetNumberOfLevels(_header.NumRecords+1) != _levels)
            {
                RebuildTree(_header.NumRecords+1, extent);
            }

            _header.AddFeature(fid, geometry);
            Root.Insert(ToSbnFeature(fid, geometry));
        }

        private void RebuildTree(int numFeatures, Envelope extent)
        {
            var allFeatures = Root.AllFeatures();
            
            BuildTree(numFeatures, extent);
            foreach (var feature in allFeatures)
            {
                Insert(feature);
            }
        }

        private SbnFeature ToSbnFeature(uint fid, IGeometry geometry)
        {
            return new SbnFeature(_header.Envelope, fid, geometry.EnvelopeInternal);
        }

        private SbnFeature ToSbnFeature(uint fid, Envelope envelope)
        {
            return new SbnFeature(_header.Envelope, fid, envelope);
        }
        public void RemoveFeature(uint fid, IGeometry geometry)
        {
            if (!_featureIds.Contains(fid))
                throw new ArgumentOutOfRangeException("fid", "No feature with this id in tree");

            var feature = ToSbnFeature(fid, geometry);
            Root.Remove(feature);
        }

        public IEnumerable<uint> QueryFeatureIds(Envelope extent)
        {
            extent = _header.Envelope.Intersection(extent);
            var feature = ToSbnFeature(0, extent);

            return Root.QueryFeatureIds(feature.MinX, feature.MinY, 
                                        feature.MaxX, feature.MaxY);
        }
    }
}