using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using GeoAPI.Geometries;
using NUnit.Framework;
using SharpMap.Data;
using SharpMap.Data.Providers;

namespace SbnSharp.Test
{
    public class SbnTest
    {
        public SbnTest()
        {
            GeoAPI.GeometryServiceProvider.Instance = NetTopologySuite.NtsGeometryServices.Instance;

        }
        [TestCase("data\\riksgrs.sbn")]
        [TestCase("data\\road_r.sbn")]
        public void Test(string sbnFile)
        {
            Sbn sbn = null;
            Assert.DoesNotThrow(() => sbn = Sbn.Load(sbnFile));
            Assert.IsNotNull(sbn);

            var sbnTestFile = Path.ChangeExtension(sbnFile, null) + "_test.sbn";
            Assert.DoesNotThrow(() => sbn.Save(sbnTestFile));
            var fiO = new FileInfo(sbnFile);
            var fiT = new FileInfo(sbnTestFile);

            Assert.AreEqual(fiO.Length, fiT.Length);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shpFile"></param>
        [TestCase("data\\road_r.shp")]
        public void TestCreateSbn(string shpFile)
        {
            using (var p = new ShapeFile(shpFile, false))
            {
                p.Open();
                var extent = p.GetExtents();
                
                var fds = new FeatureDataSet();
                p.ExecuteIntersectionQuery(extent, fds);
                p.Close();

                var fdt = fds.Tables[0];
                var tree = SbnTree.Create(GetFeatures(fdt));

                Assert.AreEqual(fdt.Rows.Count, tree.Root.Count);

                Assert.AreEqual(fdt.Rows.Count, tree.QueryFeatureIds(extent).Count());
                var shrunk = extent.Grow(-0.2*extent.Width, - 0.2*extent.Height);
                Assert.Less(tree.QueryFeatureIds(shrunk).Count(), fdt.Rows.Count);
            }
            
        }

        private static ICollection<Tuple<uint, Envelope>> GetFeatures(FeatureDataTable fdt)
        {
            var res = new List<Tuple<uint, Envelope>>(fdt.Count);
            foreach (FeatureDataRow fdr in fdt.Rows)
            {
                res.Add(new Tuple<uint, Envelope>((uint)fdr[0] + 1, fdr.Geometry.EnvelopeInternal));
            }
            return res;
        }

    }
}
