using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GeoAPI.DataStructures;
using GeoAPI.Geometries;
using NUnit.Framework;
using SharpSbn;

namespace SbnSharp.Test
{
    public class SbnTest
    {
#if Use_SbnSharp_GeoAPI
        private static Envelope ToSbn(Envelope envelope)
        {
            return envelope;
        }
        private static Interval ToSbn(Interval interval)
        {
            return interval;
        }
#else
        private static SharpSbn.DataStructures.Envelope ToSbn(Envelope envelope)
        {
            return new SharpSbn.DataStructures.Envelope(envelope.MinX, envelope.MaxX, envelope.MinY, envelope.MaxY);
        }
        private static Interval ToSbn(Interval interval)
        {
            return new SharpSbn.DataStructures.Interval(interval.Min, interval.Max);
        }
#endif

        public SbnTest()
        {
            GeoAPI.GeometryServiceProvider.Instance = NetTopologySuite.NtsGeometryServices.Instance;
        }

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

        /*
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
        private static Envelope ToSbn(Envelope extent)
        {
            return new Envelope(extent.MinX, extent.MaxX, extent.MinY, extent.MaxY);
        }

        [Pure]
        private static Interval ToSbn(Interval interval)
        {
            return Interval.Create(interval.Min, interval.Max);
        }
        */

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

        [TestCase("data\\road_R.sbn")]
        [TestCase("data\\road_R_modified.sbn")]
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

        [TestCase("data\\road_R.sbn")]
        [TestCase("data\\road_R_modified.sbn")]
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
#if HAS_SHARPMAP
#if NET20
        private static ICollection<FrameworkReplacements.Tuple<uint, Envelope>> GetFeatures(FeatureDataTable fdt)
        {
            var res = new List<FrameworkReplacements.Tuple<uint, Envelope>>(fdt.Count);
            foreach (FeatureDataRow fdr in fdt.Rows)
            {
                var env = fdr.Geometry.EnvelopeInternal;
                res.Add( FrameworkReplacements.Tuple.Create((uint)fdr[0] + 1, ToSbn(env)));
            }
            return res;
        }
#else
        private static ICollection<Tuple<uint, Envelope>> GetFeatures(FeatureDataTable fdt)
        {
            var res = new List<Tuple<uint, Envelope>>(fdt.Count);
            foreach (FeatureDataRow fdr in fdt.Rows)
            {
                var env = fdr.Geometry.EnvelopeInternal;
                res.Add( Tuple.Create((uint)fdr[0] + 1, ToSbn(env)));
            }
            return res;
        }
#endif
#endif

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
#if NET20
        private ICollection<FrameworkReplacements.Tuple<uint, Envelope>> _data;
        private static ICollection<FrameworkReplacements.Tuple<uint, Envelope>> CreateSampleData(int featureCount, Envelope extent, uint offset = 0)
        {
            var res = new List<FrameworkReplacements.Tuple<uint, Envelope>>();
#else
        private ICollection<Tuple<uint, Envelope>> _data; 
        private static ICollection<Tuple<uint, Envelope>> CreateSampleData(int featureCount, Envelope extent, uint offset = 0)
        {
            var res = new List<Tuple<uint, Envelope>>();
#endif
            var rnd = new Random(5432);
            for (uint i = 1; i <= featureCount; i++)
            {
                var x1 = extent.MinX + rnd.NextDouble()*extent.Width;
                var x2 = x1 + rnd.NextDouble()*(extent.MaxX - x1);
                var y1 = extent.MinY + rnd.NextDouble()*extent.Height;
                var y2 = y1 + rnd.NextDouble()*(extent.MaxY - y1);
                res.Add(
#if NET20
                    FrameworkReplacements.Tuple.Create(offset + i, new Envelope(x1, x2, y1, y2))
#else
                    Tuple.Create(offset+i, new Envelope(x1, x2, y1, y2))
#endif
                    );
            }
            return res;
        }

        private static readonly System.Random IntervalRandom = new Random(4326);
        private static Interval CreateInterval(Interval range)
        {
            var lo = range.Min + IntervalRandom.NextDouble()*(range.Max - range.Min);
            var hi = lo + IntervalRandom.NextDouble()*(range.Max - lo);
            return Interval.Create(lo, hi);
        }

        [Test]
        public void TestCreateAndExtend()
        {
            _data = CreateSampleData(50000, new Envelope(-100, 100, - 100, 100));
            var tree = SbnTree.Create(_data, null, null);
            tree.RebuildRequried += HandleRebuildRequired;
            _rebuildRequiredFired = false;
            tree.Insert(500002, new Envelope(-110, -100, -110, -100), null, null);

            Assert.IsTrue(_rebuildRequiredFired);
            tree = SbnTree.Create(_data, tree.ZRange, tree.MRange);
            Assert.IsTrue(tree.FeatureCount == 50001);
            Assert.IsTrue(new Envelope(-110, 100, -110, 100).Contains(tree.Extent));

            //tree = SbnTree.Create(new List<Tuple<uint, Envelope>>(tree.QueryFids(tree.Extent)))
        }

        private bool _rebuildRequiredFired;
        private void HandleRebuildRequired(object sender, SbnTreeRebuildRequiredEventArgs e)
        {
            _rebuildRequiredFired = true;
            _data.Add(
#if NET20
                FrameworkReplacements.Tuple.Create(e.Fid, e.Geometry)
#else
                Tuple.Create(e.Fid, e.Geometry)
#endif
                );
        }

        [Test]
        public void TestCreateAndExtendByMassiveNumber()
        {
            _data = CreateSampleData(50000, new Envelope(-100, 100, -100, 100));
            var tree = SbnTree.Create(_data, null, null);
            Assert.That(tree.FeatureCount == 50000);
            foreach (var tuple in CreateSampleData(50000, new Envelope(-100, 100, -100, 100), 50001))
                tree.Insert(tuple.Item1, tuple.Item2, null, null);
            Assert.IsTrue(tree.FeatureCount == 100000);
        }

        [Test]
        public void TestCreateAndExtendByMassiveNumberZ()
        {
            _data = CreateSampleData(50000, new Envelope(-100, 100, -100, 100));
            var zRange = Interval.Create(1, 10);
            var tree = SbnTree.Create(_data, zRange, null);
            Assert.AreEqual(zRange, tree.ZRange);
            
            foreach (var tuple in CreateSampleData(50000, new Envelope(-100, 100, -100, 100), 50001))
                tree.Insert(tuple.Item1, tuple.Item2, CreateInterval(Interval.Create(0, 20)), null);
            Assert.IsTrue(tree.FeatureCount == 100000);
            Assert.IsTrue(tree.ZRange.Min < 1d);
            Assert.IsTrue(tree.ZRange.Max > 10d);
            Assert.IsTrue(Interval.Create(0, 20).Contains(tree.ZRange));
        }

        [Test]
        public void TestCreateAndExtendByMassiveNumberZM()
        {
            _data = CreateSampleData(50000, new Envelope(-100, 100, -100, 100));
            var zRange = Interval.Create(1, 10);
            var mRange = Interval.Create(10, 100);
            var tree = SbnTree.Create(_data, zRange, mRange);
            Assert.AreEqual(zRange, tree.ZRange);
            Assert.AreEqual(mRange, tree.MRange);

            foreach (var tuple in CreateSampleData(50000, new Envelope(-100, 100, -100, 100), 50001))
                tree.Insert(tuple.Item1, tuple.Item2, 
                    CreateInterval(Interval.Create(0, 20)),
                    CreateInterval(Interval.Create(9, 200)));
            Assert.IsTrue(tree.FeatureCount == 100000);
            Assert.IsTrue(tree.ZRange.Min < 1d);
            Assert.IsTrue(tree.ZRange.Max > 10d);
            Assert.IsTrue(Interval.Create(0, 20).Contains(tree.ZRange));
            Assert.IsTrue(tree.MRange.Min < 10d);
            Assert.IsTrue(tree.MRange.Max > 100d);
            Assert.IsTrue(Interval.Create(9, 200).Contains(tree.MRange));
        }
    }
}

