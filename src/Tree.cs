using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SbnIndex
{
    public class Tree
    {
        //https://github.com/drwelby/hasbeen/blob/master/bextree.py
        public List<Node> nodes;
        public int levels;
        public uint firstleafid;
        public Node root;

        public Tree(int featureCount)
        {
            nodes = new List<Node>();
            levels = (int) Math.Log(((featureCount - 1)/8.0 + 1), 2) + 1;
            if (levels < 2) levels = 2;
            if (levels > 15) levels = 15;
            firstleafid = (uint)Math.Pow(2, levels - 1);

            for (uint i = 0; i < (uint) Math.Pow(2, levels); i++)
            {
                var n = new Node(i);
                nodes.Add(n);
            }
            root = nodes[1];
            root.id = 1;
            root.tree = this;
            root.split = 'x';
            root.xmin = 0;
            root.xmax = 255;
            root.ymin = 0;
            root.ymax = 255;
            root.AddSplitCoord();
            root.Grow();
        }

        public void Insert(Feature feature)
        {
            // Insert a feature into the tree
            root.Insert(feature);
        }

        public List<Bin> ToBins()
        {
            var bins = new List<Bin>();
            var b = new Bin();
            bins.Add(b);
            foreach (var node in nodes)
            {
                b = new Bin();
                b.features = node.AllFeatures();
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
            foreach (var n in nodes.GetRange(start, end + 1))
                featureCount += n.features.Count;
            return featureCount;
        }

        private void CompactSeamFeatures()
        {
            // the mystery algorithm - compaction? optimization? obfuscation?
            if (levels < 4)
                return;

            if (levels > 4)
            {
                var start = (int)firstleafid/2 - 1;
                var end = start/8;
                if (start < 3) start = 3;
                if (end < 1) end = 1;

                foreach(var node in nodes.GetRange(start, end, -1))
                {
                    var id = (int)node.id;
                    var children = nodes.GetRange(id*2, 2);
                    foreach (var child in children)
                    {
                        var cid = (int) child.id;
                        var grandchildren = nodes.GetRange(cid*2, cid*2 + 2);
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
            var start = firstleafid/2 - 1;
            var end = start/8;
            if (start < 3) start = 3;
            if (end < 1) end = 1;
            
            //for node in self.nodes[start:end:-1]:
            for (var i = end; i > start; i--)
            {
                var node = nodes[(int) i];

                //if len(node.features) > 0 and self.levels < 6:
                //   continue
                var id = node.id;
                var children = nodes.GetRange((int) id*2, 2);
                var grandchildren = nodes.GetRange((int) id*4, 4);
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
                var node = nodes[(int) i];
                var level = Math.Ceiling(Math.Log(node.id, 2));
                var id = node.id;
                var children = nodes.GetRange((int) id*2 - 1, 2);
                children.Reverse();
                var empty = false;
                var childrenfeatures = 0;
                foreach (var child in children)
                {
                    //if not child.full:
                    //    held = True
                    var cid = (int) child.id;
                    childrenfeatures += child.features.Count;
                    var grandchildren = nodes.GetRange(cid*2, 2);
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
    }

    internal static class NumPySlicing
    {
        internal static IList<T> GetRange<T>(this IList<T> self, int start, int end, int step = 1)
        {
            List<T> res = null;
            if (step < 0)
            {
                res = (List<T>)GetRange<T>(self, end, start, -step);
                res.Reverse();
                return res;
            }

            if (end < start) 
                return new List<T>(0);

            var size = (end - start + 1)/step;

            res = new List<T>(size);
            var i = start;
            while (i <= end)
            {
                res.Add(self[i]);
                i += step;
            }
            return res;
        }
    }
}