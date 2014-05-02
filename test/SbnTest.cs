using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using GeoAPI.DataStructures;
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

        //[Ignore]
        //[TestCase("data\\riksgrs.sbn")]
        //[TestCase("data\\road_r.sbn")]
        //public void OldTest(string sbnFile)
        //{
        //    Sbn sbn = null;
        //    Assert.DoesNotThrow(() => sbn = Sbn.Load(sbnFile));
        //    Assert.IsNotNull(sbn);

        //    var sbnTestFile = Path.ChangeExtension(sbnFile, null) + "_test.sbn";
        //    Assert.DoesNotThrow(() => sbn.Save(sbnTestFile));
        //    var fiO = new FileInfo(sbnFile);
        //    var fiT = new FileInfo(sbnTestFile);

        //    Assert.AreEqual(fiO.Length, fiT.Length);
        //}

        [TestCase("data\\riksgrs.sbn")]
        [TestCase("data\\road_r.sbn")]
        [TestCase("data\\S2_jarnvag_besk_polyline.sbn")]
        public void Test(string sbnFile)
        {
            if (!File.Exists(sbnFile))
                throw new IgnoreException("File '"+sbnFile+"' not found!");

            SbnTree sbn = null;
            Assert.DoesNotThrow(() => sbn = SbnTree.Load(sbnFile));
            Assert.IsNotNull(sbn);
            Assert.IsTrue(sbn.VerifyNodes());

            var sbnTestFile = Path.ChangeExtension(sbnFile, null) + "_test.sbn";
            Assert.DoesNotThrow(() => sbn.Save(sbnTestFile));

            var fiO = new FileInfo(sbnFile);
            var fiT = new FileInfo(sbnTestFile);

            Assert.AreEqual(fiO.Length, fiT.Length);
        }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="shpFile"></param>
        //[TestCase("data\\road_r.shp")]
        //[TestCase("data\\S2_jarnvag_besk_polyline.shp")]
        //public void OldTestCreateSbn(string shpFile)
        //{
        //    using (var p = new ShapeFile(shpFile, false))
        //    {
        //        p.Open();
        //        var extent = p.GetExtents();
                
        //        var fds = new FeatureDataSet();
        //        p.ExecuteIntersectionQuery(extent, fds);
        //        p.Close();

        //        var fdt = fds.Tables[0];
        //        var tree = OldSbnTree.Create(GetFeaturesBox(fdt));

        //        Console.WriteLine(tree.FeaturesInLevel(1));
        //        Console.WriteLine(tree.FeaturesInLevel(2));
        //        Console.WriteLine(tree.FeaturesInLevel(3));

        //        Assert.AreEqual(fdt.Rows.Count, tree.Root.Count);

        //        Assert.AreEqual(fdt.Rows.Count, tree.QueryFeatureIds(extent).Count());
        //        var shrunk = extent.Grow(-0.2*extent.Width, - 0.2*extent.Height);
        //        Assert.Less(tree.QueryFeatureIds(shrunk).Count(), fdt.Rows.Count);
                
        //        Console.WriteLine();
        //        tree.DescribeTree(Console.Out);
        //    }
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shpFile"></param>
        [TestCase("data\\road_r.shp")]
        [TestCase("data\\S2_jarnvag_besk_polyline.shp")]
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

                Console.WriteLine(tree.FeaturesInLevel(1));
                Console.WriteLine(tree.FeaturesInLevel(2));
                Console.WriteLine(tree.FeaturesInLevel(3));

                Assert.AreEqual(fdt.Rows.Count, tree.FeatureCount);

                Assert.AreEqual(fdt.Rows.Count, tree.QueryFids(extent).Count());
                var shrunk = extent.Grow(-0.2 * extent.Width, -0.2 * extent.Height);
                Assert.Less(tree.QueryFids(shrunk).Count(), fdt.Rows.Count);

                Console.WriteLine();
                tree.DescribeTree(Console.Out);
            }
        }

        private static ICollection<Tuple<uint, IGeometry>> GetFeatures(FeatureDataTable fdt)
        {
            var res = new List<Tuple<uint, IGeometry>>(fdt.Count);
            foreach (FeatureDataRow fdr in fdt.Rows)
            {
                res.Add( Tuple.Create((uint)fdr[0] + 1, fdr.Geometry));
            }
            return res;
        }

        private static ICollection<Tuple<uint, Envelope>> GetFeaturesBox(FeatureDataTable fdt)
        {
            var res = new List<Tuple<uint, Envelope>>(fdt.Count);
            foreach (FeatureDataRow fdr in fdt.Rows)
            {
                res.Add(new Tuple<uint, Envelope>((uint)fdr[0] + 1, fdr.Geometry.EnvelopeInternal));
            }
            return res;
        }

        [Test]
        public void TestGetNodeLevel()
        {
            var tree = new SbnTree(new SbnHeader(8, Interval.Create(-180, 180), Interval.Create(-90, 90), Interval.Create(), Interval.Create()));
            var node = new SbnNode(tree, 1);
            Assert.AreEqual(1, node.Level);

            node = new SbnNode(tree, 2);
            Assert.AreEqual(2, node.Level);
            node = new SbnNode(tree, 3);
            Assert.AreEqual(2, node.Level);

            node = new SbnNode(tree, 4);
            Assert.AreEqual(3, node.Level);
            node = new SbnNode(tree, 5);
            Assert.AreEqual(3, node.Level);
            node = new SbnNode(tree, 6);
            Assert.AreEqual(3, node.Level);
            node = new SbnNode(tree, 7);
            Assert.AreEqual(3, node.Level);

            node = new SbnNode(tree, 8);
            Assert.AreEqual(4, node.Level);
            node = new SbnNode(tree, 9);
            Assert.AreEqual(4, node.Level);
            node = new SbnNode(tree, 10);
            Assert.AreEqual(4, node.Level);
            node = new SbnNode(tree, 11);
            Assert.AreEqual(4, node.Level);
            node = new SbnNode(tree, 12);
            Assert.AreEqual(4, node.Level);
            node = new SbnNode(tree, 13);
            Assert.AreEqual(4, node.Level);
            node = new SbnNode(tree, 14);
            Assert.AreEqual(4, node.Level);
            node = new SbnNode(tree, 15);
            Assert.AreEqual(4, node.Level);
        }

        //[TestCase("data\\road_r.sbx")]
        //public void TestSbx(string sbx)
        //{
        //    using (var r = new BinaryReader(File.OpenRead(sbx)))
        //    {
        //        var sbxObj = new Sbx(r);
        //    }

        //}
    }
}
