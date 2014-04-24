using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Xml.Serialization;

namespace SbnIndex
{
    public class Node
    {
        public uint id;
        public int count;
        public Tree tree;
        public char split;
        public Node righttop;
        public Node parent;
        public Node leftbottom;
        public Node sibling;
        public int splitcoord;
        public List<Feature> features;
        public List<Feature> holdfeatures;
        public bool full;
        public int xmin, xmax, ymin, ymax;

        public Node(uint u)
        {
            id = u;
            count = 0;
            features = new List<Feature>();
            holdfeatures = new List<Feature>();
            full = false;
            sibling = null;
        }

        public override string ToString()
        {
            return string.Format("Node {0}: ({1}-{2}, {3}{4})/{5}", id, xmin, xmax, ymin, ymax, splitcoord);
        }

        public void AddSplitCoord()
        {
            int mid;
            if (split == 'x')
                mid = (int) ((xmin + xmax)/2.0) + 1;
            else
                mid = (int) ((ymin + ymax)/2.0) + 1;
            splitcoord = mid - mid%2;
        }

        public void AddChildren()
        {
            // first child node
            var rt = tree.nodes[(int)id*2];
            rt.tree = tree;
            if (split == 'x')
            {
                rt.xmin = (int) splitcoord + 1;
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
                rt.ymin = (int) splitcoord + 1;
                rt.ymax = ymax;
                rt.split = 'x';
                rt.AddSplitCoord(); 
            }
            rt.parent = this;
            righttop = rt;
            
            //second child node
            var lb = tree.nodes[(int) id*2 + 1];
            lb.tree = tree;
            if (split == 'x')
            {
                lb.xmax = (int) splitcoord;
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
                lb.ymax = (int) splitcoord;
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
            if (id >= tree.firstleafid) 
                return;
            AddChildren();
            righttop.Grow();
            leftbottom.Grow();
        }

        public void Insert(Feature feature)
        {
            // if this is leaf, just take the feature
            if (id >= tree.firstleafid)
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
                        return;
                    }
                }

            }
            // The node is split so we can sort features
            int min, max, smin, smax;
            if (split == 'x')
            {
                min = feature.xmin;
                max = feature.xmax;
                smin = feature.ymin;
                smax = feature.ymax;
            }
            else
            {
                min = feature.ymin;
                max = feature.ymax;
                smin = feature.xmin;
                smax = feature.xmax;
            }
            // Grab features on the seam we can't split
            if (min <= splitcoord && max > splitcoord)
            {
                features.Add(feature);
                return;
            }
            else
            {
                PassFeature(feature);
            }
        }

        public List<Feature> AllFeatures()
        {
            // return all the features in the node
            if (id >= tree.firstleafid)
                return features;
            if (id == 1)
                return features;
            if (holdfeatures.Count <= 8)
                return holdfeatures;
            return features;
        }


        public int SiblingFeatureCount()
        {
            // return the number of features of a node and its sibling
            return AllFeatures().Count + sibling.AllFeatures().Count;
        }



        private void PassFeature(Feature feature)
        {
            // pass the feature to a child node
            int min, max;
            if (split == 'x')
            {
                min = feature.xmin;
                max = feature.xmax;
            }
            else
            {
                min = feature.ymin;
                max = feature.ymax;
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
    }

}