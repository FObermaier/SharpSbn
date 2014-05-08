using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using SharpSbn;

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
            if (!File.Exists(shpFile))
                throw new IgnoreException("File '" + shpFile + "' not found!");

            // restore original data
            var sbx = Path.ChangeExtension(shpFile, "sbx");
            var sbn = Path.ChangeExtension(shpFile, "sbn");
            if (File.Exists(sbx + "_orig"))
                File.Copy(sbx + "_orig", sbx, true);
            if (File.Exists(sbn + "_orig"))
                File.Copy(sbn + "_orig", sbn, true);

            using (var p = new ShapeFile(shpFile, false))
            {
                p.Open();
                var extent = p.GetExtents();

                var fds = new FeatureDataSet();
                p.ExecuteIntersectionQuery(extent, fds);
                p.Close();

                var fdt = fds.Tables[0];
                var tree = SbnTree.Create(GetFeatures(fdt));

                Assert.AreEqual(fdt.Rows.Count, tree.FeatureCount);

                Assert.AreEqual(fdt.Rows.Count, tree.QueryFids(extent).Count());
                var shrunk = extent.Grow(-0.4 * extent.Width, -0.3 * extent.Height);
                Assert.Less(tree.QueryFids(shrunk).Count(), fdt.Rows.Count);

                Console.WriteLine();
                tree.DescribeTree(Console.Out);

                //Save the tree
                File.Copy(sbx, sbx + "_orig", true);
                File.Copy(sbn, sbn + "_orig", true);

                tree.Save(sbn);

                SbnTree tree2 = null;
                Assert.DoesNotThrow(() => tree2 = SbnTree.Load(sbn));
                Assert.IsNotNull(tree2);

                Assert.AreEqual(tree.FeatureCount, tree2.FeatureCount);
                Assert.AreEqual(tree.NumLevels, tree2.NumLevels);
                for (var i = 1; i < tree.NumLevels; i++)
                    Assert.AreEqual(tree.GetNodesOfLevel(i), tree2.GetNodesOfLevel(i));

                Assert.AreEqual(tree.QueryFids(shrunk).Count(), tree2.QueryFids(shrunk).Count());

                SbnTree.SbnToText(sbn, new StreamWriter(File.OpenWrite(Path.ChangeExtension(sbn, ".createdsbn.txt"))));
            }
        }

        [TestCase("data\\riksgrs.sbn")]
        [TestCase("data\\road_r.sbn")]
        [TestCase("data\\road_r_modified.sbn")]
        [TestCase("data\\S2_jarnvag_besk_polyline.sbn")]
        public void TestToText(string sbnFile)
        {
            if (!File.Exists(sbnFile))
                throw new IgnoreException("File '" + sbnFile + "' not found!");

            SbnTree sbn = null;
            Assert.DoesNotThrow(() => sbn = SbnTree.Load(sbnFile));
            Assert.DoesNotThrow(() => SbnTree.SbnToText(sbnFile, new StreamWriter(File.OpenWrite(Path.ChangeExtension(sbnFile, ".sbn.txt")))));
            Assert.IsNotNull(sbn);
            Assert.IsTrue(sbn.VerifyNodes());

            var sbnTestFile = Path.ChangeExtension(sbnFile, null) + "_test.sbn";
            Assert.DoesNotThrow(() => sbn.Save(sbnTestFile));
            Assert.DoesNotThrow(() => SbnTree.SbnToText(sbnTestFile, new StreamWriter(File.OpenWrite(Path.ChangeExtension(sbnTestFile, ".sbn.txt")))));
        }

        [TestCase("data\\road_r.sbn")]
        [TestCase("data\\road_r_modified.sbn")]
        public void TestQueryTime(string sbnFile)
        {
            if (!File.Exists(sbnFile))
                throw new IgnoreException("File '" + sbnFile + "' not found!");

            var sw = new Stopwatch();
            SbnTree sbn = null;
            sw.Start();
            Assert.DoesNotThrow(() => sbn = SbnTree.Load(sbnFile));
            sw.Stop();
            Console.WriteLine("SbnTree read in {0:N0} ticks", sw.ElapsedTicks);

            var fullExtent = sbn.Extent;
            for (var i = 0; i < 10; i++)
            {
                sw.Restart();
                var fids = new List<uint>(sbn.QueryFids(fullExtent));
                sw.Stop();
                Console.WriteLine("Querying full in {0:N0} ticks ({1} ids)", sw.ElapsedTicks, fids.Count);
            }

            var partialExtent = new Envelope(
                fullExtent.MinX + 0.4 * fullExtent.Width,
                fullExtent.MaxX - 0.4 * fullExtent.Width,
                fullExtent.MinY + 0.3 * fullExtent.Height,
                fullExtent.MaxY - 0.3 * fullExtent.Height);
            for (var i = 0; i < 10; i++)
            {
                sw.Restart();
                var fids = new List<uint>(sbn.QueryFids(partialExtent));
                sw.Stop();
                Console.WriteLine("Querying part in {0:N0} ticks ({1} ids)", sw.ElapsedTicks, fids.Count);
            }
        }

        [TestCase("data\\road_r.sbn")]
        [TestCase("data\\road_r_modified.sbn")]
        public void TestQueryTime2(string sbnFile)
        {
            if (!File.Exists(sbnFile))
                throw new IgnoreException("File '" + sbnFile + "' not found!");

            var sw = new Stopwatch();
            SbnQueryOnlyTree sbn = null;
            SbnQueryOnlyTree.DefaultMaxCacheLevel = 24;
            sw.Start();
            Assert.DoesNotThrow(() => sbn = SbnQueryOnlyTree.Open(sbnFile));
            sw.Stop();
            Console.WriteLine("SbnTree read in {0:N0} ticks", sw.ElapsedTicks);

            var fullExtent = sbn.Extent;
            for (var i = 0; i < 10; i++)
            {
                sw.Restart();
                var fids = new List<uint>(sbn.QueryFids(fullExtent));
                sw.Stop();
                Console.WriteLine("Querying full in {0:N0} ticks ({1} ids)", sw.ElapsedTicks, fids.Count);
            }

            var partialExtent = new Envelope(
                fullExtent.MinX + 0.4 * fullExtent.Width,
                fullExtent.MaxX - 0.4 * fullExtent.Width,
                fullExtent.MinY + 0.3 * fullExtent.Height,
                fullExtent.MaxY - 0.3 * fullExtent.Height);
            for (var i = 0; i < 10; i++)
            {
                sw.Restart();
                var fids = new List<uint>(sbn.QueryFids(partialExtent));
                sw.Stop();
                Console.WriteLine("Querying part in {0:N0} ticks ({1} ids)", sw.ElapsedTicks, fids.Count);
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
