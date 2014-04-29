using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using GeoAPI.DataStructures;
using GeoAPI.Geometries;

namespace SbnSharp
{
    public class SbnTree
    {
        /// <summary>
        /// Method to create a tree from a collection of
        /// </summary>
        /// <param name="idExtents"></param>
        /// <returns></returns>
        public static SbnTree Create(ICollection<Tuple<uint, Envelope>> idExtents)
        {
            var extent = GetExtents(idExtents);
            var sbnTree = new SbnTree(idExtents.Count, extent);
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
        internal List<SbnNode> Nodes { get; private set; }
        private int _levels;
        private readonly HashSet<uint> _featureIds = new HashSet<uint>();

        internal int FirstLeafId { get; private set; }
        
        /// <summary>
        /// Gets a value indicating the root node
        /// </summary>
        public SbnNode Root { get; private set; }



        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="featureCount">The initial number of features</param>
        public SbnTree(int featureCount, Envelope extent)
        {
            BuildTree(featureCount, extent);
        }

        private void BuildTree(int featureCount, Envelope extent)
        {
            _header = new SbnHeader(featureCount, extent);
            
            Nodes = new List<SbnNode>();
            _levels = GetLevels(featureCount);
            FirstLeafId = (int)Math.Pow(2, _levels - 1);

            for (int i = 0; i < (uint) Math.Pow(2, _levels); i++)
            {
                var n = new SbnNode(this, i);
                Nodes.Add(n);
            }
            Root = Nodes[1];
            //Root.id = 1;
            //Root.tree = this;
            Root.split = 'x';
            Root.xmin = 0;
            Root.xmax = 255;
            Root.ymin = 0;
            Root.ymax = 255;
            Root.AddSplitCoord();
            Root.Grow();
        }

        private static int GetLevels(int featureCount)
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

        internal List<SbnBin> ToBins()
        {
            var bins = new List<SbnBin>();
            var bid = 0;
            var b = new SbnBin(bid++);
            bins.Add(b);
            foreach (var node in Nodes)
            {
                b = new SbnBin(bid++);
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
            var end = (int) 2*start - 1;
            var featureCount = 0;
            foreach (var n in Nodes.GetRange(start, end + 1))
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
                    var id = (int)node.id;
                    var children = Nodes.GetRange(id*2, 2);
                    foreach (var child in children)
                    {
                        // There are no items to pull up
                        if (child.Count == 0) continue;

                        var cid = (int) child.id;
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

        private void CompactSeamFeatures2()
        {
            // another run at the mystery algorithm

            //for node in self.nodes[1:self.firstleafid/4]:
            var start = FirstLeafId/2 - 1;
            var end = start/8;
            if (start < 3) start = 3;
            if (end < 1) end = 1;
            
            //for node in self.nodes[start:end:-1]:
            for (var i = end; i > start; i--)
            {
                var node = Nodes[(int) i];

                //if len(node.features) > 0 and self.levels < 6:
                //   continue
                var id = node.id;
                var children = Nodes.GetRange((int) id*2, 2);
                var grandchildren = Nodes.GetRange((int) id*4, 4);
                var gccount = 0;
                foreach (var gcnode in grandchildren)
                    gccount += gcnode.AllFeatures().Count;
                //print "Node %s has %s GC" % (id,gccount)
                if (gccount == 0)
                {
                    foreach (var cnode in children)
                    {
                        if (cnode.AllFeatures().Count + node.features.Count > 8)
                            continue;
                        //print "Slurping %s features from node %s" % (len(cnode.features),cnode.id)
                        node.features.AddRange(cnode.AllFeatures());
                        //node.features.extend(cnode.features)
                        cnode.features.Clear();
                        cnode.holdfeatures.Clear();
                    }
                }
            }
            // compact unsplit nodes see cities/248
            return;

            //for node in self.nodes[start:end:-1]:
            for (var i = end; i > start; i--)
            {
                var node = Nodes[(int) i];
                var level = Math.Ceiling(Math.Log(node.id, 2));
                var id = node.id;
                var children = Nodes.GetRange((int) id*2 - 1, 2);
                children.Reverse();
                var empty = false;
                var childrenfeatures = 0;
                foreach (var child in children)
                {
                    //if not child.full:
                    //    held = True
                    var cid = (int) child.id;
                    childrenfeatures += child.features.Count;
                    var grandchildren = Nodes.GetRange(cid*2, 2);
                    foreach (var gcnode in grandchildren)
                    {
                        if (gcnode.features.Count == 0)
                            empty = true;
                    }
                }
                //print "Node %s childless: %s" % (cid,empty)
                Debug.WriteLine("{0} : {1}", empty, childrenfeatures);
                if (empty && childrenfeatures > 0)
                {
                    //node.features.extend(child.features)
                    foreach (var child in children)
                    {
                        if (child.SiblingFeatureCount() < 4 &&
                            child.SiblingFeatureCount() > 0)
                            continue;
                        //if self.featuresinlevel(level) >= 8:
                        //    return
                        //print "Slurping %s features from node %s" % (len(child.allfeatures()),child.id)
                        node.features.AddRange(child.AllFeatures());
                        //node.full = True
                        child.features.Clear();
                        child.holdfeatures.Clear();
                    }
                }
                return;
            }
        }

        public void AddFeature(uint fid, IGeometry geometry)
        {
            if (_featureIds.Contains(fid))
                throw new InvalidOperationException("A feature with this id is already present");

            var extent = geometry.EnvelopeInternal;
            if (!_header.Envelope.Contains(extent) || GetLevels(_header.NumRecords+1) != _levels)
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