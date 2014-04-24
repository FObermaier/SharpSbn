using System.Collections.Generic;
using System.Security.Policy;
using System.Text;
using System.Xml.Serialization;

namespace SbnIndex
{
    public class Bin
    {
        public uint id;
        public List<Feature> features;
        public int numFeat;
    }
}
