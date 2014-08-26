using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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
        [TestCase("data\\rivers.shp")]
        [TestCase("data\\countries.shp")]
        [TestCase("data\\cities.shp")]
        public void TestCreateSbn(string shpFile)
        {
            if (!File.Exists(shpFile))
                throw new IgnoreException("File '" + shpFile + "' not found!");

            var fds = new FeatureDataSet();
            Envelope extent;
            Collection<uint> oidsInView;
            using (var p = new ShapeFile(shpFile, false))
            {
                p.Open();
                extent = p.GetExtents();
                p.ExecuteIntersectionQuery(extent, fds);
                oidsInView = p.GetObjectIDsInView(extent);
            }
            var fdt = fds.Tables[0];

            // restore original data
            var sbx = Path.ChangeExtension(shpFile, "sbx");
            var sbn = Path.ChangeExtension(shpFile, "sbn");

            if (File.Exists(sbx + "_orig"))
                File.Copy(sbx + "_orig", sbx, true);
            if (File.Exists(sbn + "_orig"))
                File.Copy(sbn + "_orig", sbn, true);
                
            var tree = SbnTree.Create(GetFeatures(fdt), null, null);
            tree.Save(sbn);

            Assert.AreEqual(fdt.Rows.Count, tree.FeatureCount);
            Assert.AreEqual(fdt.Rows.Count, tree.QueryFids(ToSbn(extent)).Count());


            fds = new FeatureDataSet();
            extent = extent.Grow(-0.4*extent.Width, -0.3*extent.Height);
            using (var p = new ShapeFile(shpFile, false))
            {
                p.Open();
                p.ExecuteIntersectionQuery(extent, fds);
                oidsInView = p.GetObjectIDsInView(extent);
            }


            var shrunk = ToSbn(extent);
            var sbnOids = new HashSet<uint>(tree.QueryFids(shrunk));
            foreach (var oid in oidsInView)
                Assert.IsTrue(sbnOids.Contains(oid+1));

            Console.WriteLine();
                tree.DescribeTree(Console.Out);

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

        [Pure]
        private static SharpSbn.DataStructures.Envelope ToSbn(Envelope extent)
        {
            return new SharpSbn.DataStructures.Envelope(extent.MinX, extent.MaxX, extent.MinY, extent.MaxY);
        }

        [Pure]
        private static SharpSbn.DataStructures.Interval ToSbn(Interval interval)
        {
            return SharpSbn.DataStructures.Interval.Create(interval.Min, interval.Max);
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

            var partialExtent = ToSbn(new Envelope(
                fullExtent.MinX + 0.4 * fullExtent.Width,
                fullExtent.MaxX - 0.4 * fullExtent.Width,
                fullExtent.MinY + 0.3 * fullExtent.Height,
                fullExtent.MaxY - 0.3 * fullExtent.Height));
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
            SbnQueryOnlyTree.DefaultMaxCacheLevel = 12;
            sw.Start();
            Assert.DoesNotThrow(() => sbn = SbnQueryOnlyTree.Open(sbnFile));
            sw.Stop();
            Console.WriteLine("SbnQueryOnlyTree opened in {0:N0} ticks", sw.ElapsedTicks);

            var fullExtent = sbn.Extent;
            for (var i = 0; i < 10; i++)
            {
                sw.Restart();
                var fids = new List<uint>(sbn.QueryFids(fullExtent));
                sw.Stop();
                Console.WriteLine("Querying full in {0:N0} ticks ({1} ids)", sw.ElapsedTicks, fids.Count);
            }

            var partialExtent = ToSbn(new Envelope(
                fullExtent.MinX + 0.4 * fullExtent.Width,
                fullExtent.MaxX - 0.4 * fullExtent.Width,
                fullExtent.MinY + 0.3 * fullExtent.Height,
                fullExtent.MaxY - 0.3 * fullExtent.Height));
            for (var i = 0; i < 10; i++)
            {
                sw.Restart();
                var fids = new List<uint>(sbn.QueryFids(partialExtent));
                sw.Stop();
                Console.WriteLine("Querying part in {0:N0} ticks ({1} ids)", sw.ElapsedTicks, fids.Count);
            }
        }

        private static ICollection<Tuple<uint, SharpSbn.DataStructures.Envelope>> GetFeatures(FeatureDataTable fdt)
        {
            var res = new List<Tuple<uint, SharpSbn.DataStructures.Envelope>>(fdt.Count);
            foreach (FeatureDataRow fdr in fdt.Rows)
            {
                var env = fdr.Geometry.EnvelopeInternal;
                res.Add( Tuple.Create((uint)fdr[0] + 1, ToSbn(env)));
            }
            return res;
        }

        [Test]
        public void TestGetNodeLevel()
        {
            var tree = new SbnTree(new SbnHeader(256, ToSbn(Interval.Create(-180, 180)),
                                                      ToSbn(Interval.Create(-90, 90)), 
                                                      ToSbn(Interval.Create()),
                                                      ToSbn(Interval.Create())));
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
        private static ICollection<Tuple<uint, SharpSbn.DataStructures.Envelope>> CreateSampleData(int featureCount, SharpSbn.DataStructures.Envelope extent, uint offset = 0)
        {
            var res = new List<Tuple<uint, SharpSbn.DataStructures.Envelope>>();
            var rnd = new Random(5432);
            for (uint i = 1; i <= featureCount; i++)
            {
                var x1 = extent.MinX + rnd.NextDouble()*extent.Width;
                var x2 = x1 + rnd.NextDouble()*(extent.MaxX - x1);
                var y1 = extent.MinY + rnd.NextDouble()*extent.Height;
                var y2 = y1 + rnd.NextDouble()*(extent.MaxY - y1);
                res.Add(Tuple.Create(offset+i, new SharpSbn.DataStructures.Envelope(x1, x2, y1, y2)));
            }
            return res;
        }

        private static readonly System.Random IntervalRandom = new Random(4326);
        private static SharpSbn.DataStructures.Interval CreateInterval(SharpSbn.DataStructures.Interval range)
        {
            var lo = range.Min + IntervalRandom.NextDouble()*(range.Max - range.Min);
            var hi = lo + IntervalRandom.NextDouble()*(range.Max - lo);
            return SharpSbn.DataStructures.Interval.Create(lo, hi);
        }

        [Test]
        public void TestCreateAndExtend()
        {
            _data = CreateSampleData(50000, new SharpSbn.DataStructures.Envelope(-100, 100, - 100, 100));
            var tree = SbnTree.Create(_data, null, null);
            tree.RebuildRequried += HandleRebuildRequired;
            _rebuildRequiredFired = false;
            tree.Insert(500002, new SharpSbn.DataStructures.Envelope(-110, -100, -110, -100), null, null);

            Assert.IsTrue(_rebuildRequiredFired);
            tree = SbnTree.Create(_data, tree.ZRange, tree.MRange);
            Assert.IsTrue(tree.FeatureCount == 50001);
            Assert.IsTrue(new SharpSbn.DataStructures.Envelope(-110, 100, -110, 100).Contains(tree.Extent));

            //tree = SbnTree.Create(new List<Tuple<uint, SharpSbn.DataStructures.Envelope>>(tree.QueryFids(tree.Extent)))
        }

        private bool _rebuildRequiredFired;
        private ICollection<Tuple<uint, SharpSbn.DataStructures.Envelope>> _data; 
        private void HandleRebuildRequired(object sender, SbnTreeRebuildRequiredEventArgs e)
        {
            _rebuildRequiredFired = true;
            _data.Add(Tuple.Create(e.Fid, e.Geometry));
        }

        [Test]
        public void TestCreateAndExtendByMassiveNumber()
        {
            _data = CreateSampleData(50000, new SharpSbn.DataStructures.Envelope(-100, 100, -100, 100));
            var tree = SbnTree.Create(_data, null, null);

            foreach (var tuple in CreateSampleData(50000, new SharpSbn.DataStructures.Envelope(-100, 100, -100, 100), 50001))
                tree.Insert(tuple.Item1, tuple.Item2, null, null);
            Assert.IsTrue(tree.FeatureCount == 100000);
        }

        [Test]
        public void TestCreateAndExtendByMassiveNumberZ()
        {
            _data = CreateSampleData(50000, new SharpSbn.DataStructures.Envelope(-100, 100, -100, 100));
            var zRange = SharpSbn.DataStructures.Interval.Create(1, 10);
            var tree = SbnTree.Create(_data, zRange, null);
            Assert.AreEqual(zRange, tree.ZRange);
            
            foreach (var tuple in CreateSampleData(50000, new SharpSbn.DataStructures.Envelope(-100, 100, -100, 100), 50001))
                tree.Insert(tuple.Item1, tuple.Item2, CreateInterval(SharpSbn.DataStructures.Interval.Create(0, 20)), null);
            Assert.IsTrue(tree.FeatureCount == 100000);
            Assert.IsTrue(tree.ZRange.Min < 1d);
            Assert.IsTrue(tree.ZRange.Max > 10d);
            Assert.IsTrue(SharpSbn.DataStructures.Interval.Create(0, 20).Contains(tree.ZRange));
        }

        [Test]
        public void TestCreateAndExtendByMassiveNumberZM()
        {
            _data = CreateSampleData(50000, new SharpSbn.DataStructures.Envelope(-100, 100, -100, 100));
            var zRange = SharpSbn.DataStructures.Interval.Create(1, 10);
            var mRange = SharpSbn.DataStructures.Interval.Create(10, 100);
            var tree = SbnTree.Create(_data, zRange, mRange);
            Assert.AreEqual(zRange, tree.ZRange);
            Assert.AreEqual(mRange, tree.MRange);

            foreach (var tuple in CreateSampleData(50000, new SharpSbn.DataStructures.Envelope(-100, 100, -100, 100), 50001))
                tree.Insert(tuple.Item1, tuple.Item2, 
                    CreateInterval(SharpSbn.DataStructures.Interval.Create(0, 20)),
                    CreateInterval(SharpSbn.DataStructures.Interval.Create(9, 200)));
            Assert.IsTrue(tree.FeatureCount == 100000);
            Assert.IsTrue(tree.ZRange.Min < 1d);
            Assert.IsTrue(tree.ZRange.Max > 10d);
            Assert.IsTrue(SharpSbn.DataStructures.Interval.Create(0, 20).Contains(tree.ZRange));
            Assert.IsTrue(tree.MRange.Min < 10d);
            Assert.IsTrue(tree.MRange.Max > 100d);
            Assert.IsTrue(SharpSbn.DataStructures.Interval.Create(9, 200).Contains(tree.MRange));
        }
    }
}
