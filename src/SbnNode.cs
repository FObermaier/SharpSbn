using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Xml.Serialization;

namespace SbnSharp
{
    public class SbnNode
    {
        public int id { get; private set; }
        public int count;
        public SbnTree tree;
        public char split;
        public SbnNode righttop;

        public SbnNode Child1
        {
            get
            {
                if (id >= tree.FirstLeafId)
                    return null;
                return tree.Nodes[(int)id*2];
            }
        }

        public SbnNode parent;
        public SbnNode Parent
        {
            get
            {
                if (id == 1)
                    return null;

                var firstSiblingId = (int)id - (int)id%2;
                return tree.Nodes[firstSiblingId/2];
            }
        }

    
        public SbnNode leftbottom;
        public SbnNode Child2
        {
            get
            {
                if (id >= tree.FirstLeafId)
                    return null;
                return tree.Nodes[(int)id * 2 + 1];
            }
        }

        public SbnNode sibling;

        public SbnNode Sibling
        {
            get
            {
                if (id == 1)
                    return null;

                if (id - id%2 == id)
                    return tree.Nodes[(int)id + 1];
                return tree.Nodes[(int) id - 1];
            }
        }

        public /*int*/ byte splitcoord;
        public List<SbnFeature> features;
        public List<SbnFeature> holdfeatures;
        public bool full;
        public /*int*/ byte xmin, xmax, ymin, ymax;

        internal SbnNode(SbnTree tree, int u)
        {
            this.tree = tree;
            id = u;
            count = 0;
            features = new List<SbnFeature>();
            holdfeatures = new List<SbnFeature>();
            full = false;
            sibling = null;
        }

        public override string ToString()
        {
            return string.Format("[Node {0}: ({1}-{2}, {3}-{4})/{5}]", id, xmin, xmax, ymin, ymax, splitcoord);
        }

        public void AddSplitCoord()
        {
            int mid;
            if (split == 'x')
                mid = /*(int)*/ (byte)((xmin + xmax)/2.0) + 1;
            else
                mid = /*(int)*/ (byte)((ymin + ymax) / 2.0) + 1;
            splitcoord = (byte)(mid - mid%2);
        }

        public void AddChildren()
        {
            // first child node
            var rt = tree.Nodes[(int)id*2];
            rt.tree = tree;
            if (split == 'x')
            {
                rt.xmin = /*(int)*/ (byte)(splitcoord + 1);
                rt.xmax = xmax;
                rt.ymin = ymin;
                rt.ymax = ymax;
                rt.split = 'y';
                rt.AddSplitCoord();
            }
            else
            {
                rt.xmin = xmin;
                rt.xmax = xmax;
                rt.ymin = /*(int)*/ (byte)(splitcoord + 1);
                rt.ymax = ymax;
                rt.split = 'x';
                rt.AddSplitCoord(); 
            }
            rt.parent = this;
            righttop = rt;
            
            //second child node
            var lb = tree.Nodes[(int) id*2 + 1];
            lb.tree = tree;
            if (split == 'x')
            {
                lb.xmax = /*(int)*/ splitcoord;
                lb.xmin = xmin;
                lb.ymin = ymin;
                lb.ymax = ymax;
                lb.split = 'y';
                lb.AddSplitCoord();
            }
            else
            {
                lb.xmin = xmin;
                lb.xmax = xmax;
                lb.ymax = /*(int)*/ splitcoord;
                lb.ymin = ymin;
                lb.split = 'x';
                lb.AddSplitCoord();
            }
            lb.parent = this;
            leftbottom = lb;
            lb.sibling = rt;
            rt.sibling = lb;
        }

        public void Grow()
        {
            //recursively grow the tree
            if (id >= tree.FirstLeafId) 
                return;
            AddChildren();
            righttop.Grow();
            leftbottom.Grow();
        }

        public void Insert(SbnFeature feature)
        {
            // if this is leaf, just take the feature
            if (id >= tree.FirstLeafId)
            {
                features.Add(feature);
                return;
            }

            // it takes 8 features to split a node
            // so we'll hold 8 features first
            if (id > 1)
            {
                if (!full)
                {
                    if (holdfeatures.Count < 8)
                    {
                        holdfeatures.Add(feature);
                        return;
                    }
                    if (holdfeatures.Count == 8)
                    {
                        full = true;
                        holdfeatures.Add(feature);
                        foreach (var holdfeature in holdfeatures)
                        {
                            Insert(holdfeature);
                        }
                        holdfeatures.Clear();
                        return;
                    }
                }

            }

            // The node is split so we can sort features
            int min, max; //, smin, smax;
            if (split == 'x')
            {
                min = feature.MinX;
                max = feature.MaxX;
                //smin = feature.MinY;
                //smax = feature.MaxY;
            }
            else
            {
                min = feature.MinY;
                max = feature.MaxY;
                //smin = feature.MinX;
                //smax = feature.MaxX;
            }

            // Grab features on the seam we can't split
            if (min <= splitcoord && max > splitcoord)
            {
                features.Add(feature);
                return;
            }
            PassFeature(feature);
        }

        public List<SbnFeature> AllFeatures()
        {
            // return all the features in the node
            if (id >= tree.FirstLeafId)
                return features;
            if (id == 1)
                return features;
            if (!full) //holdfeatures.Count <= 8)
                return holdfeatures;
            return features;
        }


        /// <summary>
        /// Get the number of features
        /// </summary>
        public int Count
        {
            get
            {
                if (id >= tree.FirstLeafId)
                    return features.Count;

                var tmpCount = id == 1
                    ? features.Count
                    : !full ? holdfeatures.Count : features.Count;
                
                return tmpCount + righttop.Count + leftbottom.Count;
            }
        }

        /// <summary>
        /// Method to get the number of features of this node and its siblings
        /// </summary>
        /// <returns></returns>
        public int SiblingFeatureCount()
        {
            // return the number of features of a node and its sibling
            return AllFeatures().Count + sibling.AllFeatures().Count;
        }


        /// <summary>
        /// Method to pass a feature to the base nodes
        /// </summary>
        /// <param name="feature"></param>
        private void PassFeature(SbnFeature feature)
        {
            // pass the feature to a child node
            int min, max;
            if (split == 'x')
            {
                min = feature.MinX;
                max = feature.MaxX;
            }
            else
            {
                min = feature.MinY;
                max = feature.MaxY;
            }
            if (min < splitcoord)
            {
                leftbottom.Insert(feature);
            }
            else
            {
                righttop.Insert(feature);
            }
        }

        public IEnumerable<uint> QueryFeatureIds(byte minX, byte minY, byte maxX, byte maxY)
        {
            /*
            if (Contains(minX, minY, maxX, maxY))
            {
                var res = new List<uint>(Count);
                res.AddRange(features.Select(feature => feature.Fid));
                res.AddRange(holdfeatures.Select(feature => feature.Fid));
                res.AddRange(righttop.QueryFeatureIds());
            }
             */

            foreach (var feature in features)
            {
                if (feature.Intersects(minX, maxX, minY, maxY))
                    yield return feature.Fid;
            }

            // If we are at the leaf level, we don't have any holdfeatures anymore
            if (id >= tree.FirstLeafId)
                yield break;

            foreach (var feature in holdfeatures)
            {
                if (feature.Intersects(minX, maxX, minY, maxY))
                    yield return feature.Fid;
            }

            if (righttop.Intersects(minX, minY, maxX, maxY))
            {
                foreach (var feature in righttop.QueryFeatureIds(minX, minY, maxX, maxY))
                {
                    yield return feature;
                }
            }

            if (leftbottom.Intersects(minX, minY, maxX, maxY))
            {
                foreach (var feature in leftbottom.QueryFeatureIds(minX, minY, maxX, maxY))
                {
                    yield return feature;
                }
            }
        }

        public void Remove(SbnFeature feature)
        {
            if (features.Contains(feature))
            {
                features.Remove(feature);
                return;
            }
            if (holdfeatures.Contains(feature))
            {
                features.Remove(feature);
                return;
            }

            if (righttop.Intersects(feature.MinX, feature.MinY, feature.MaxX, feature.MaxY))
            {
                righttop.Remove(feature);
                return;
            }

            if (leftbottom.Intersects(feature.MinX, feature.MinY, feature.MaxX, feature.MaxY))
            {
                leftbottom.Remove(feature);
                return;
            }
        }

        internal bool Intersects(byte minX, byte minY, byte maxX, byte maxY)
        {
            return !(minX > xmax || maxX < xmin || minY > ymax || maxY < ymin);
        }
    }

}