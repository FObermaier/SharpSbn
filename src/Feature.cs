using System.IO;

namespace SbnIndex
{
    public class Feature
    {
        public uint id;
        public int xmin, xmax, ymin, ymax;

        public Feature(BinaryReader sr)
        {
            xmin = sr.ReadInt32();
            xmax = sr.ReadInt32();
            xmin = sr.ReadInt32();
            ymax = sr.ReadInt32();
            id = sr.ReadUInt32();
            
        }
    }
}