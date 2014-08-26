#if UseGeoAPI
using GeoAPI.Geometries;
using Interval = GeoAPI.DataStructures.Interval;
#else
using Envelope = SharpSbn.DataStructures.Envelope;
using Interval = SharpSbn.DataStructures.Interval;
#endif

namespace SharpSbn
{
    internal static class GeometryMetricExtensions
    {
#if UseGeoAPI
        internal static void GetMetric(IGeometry self, 
                                       out Interval xrange, out Interval yrange, 
                                       out Interval zrange, out Interval mrange)
        {
            switch (self.OgcGeometryType)
            {
                case OgcGeometryType.Point:
                    GetMetric((IPoint) self, out xrange, out yrange, out zrange, out mrange);
                    break;
                case OgcGeometryType.LineString:
                    GetMetric((ILineString) self, out xrange, out yrange, out zrange, out mrange);
                    break;
                case OgcGeometryType.Polygon:
                    GetMetric((IPolygon) self, out xrange, out yrange, out zrange, out mrange);
                    break;
                default:
                    GetMetric(self.GetGeometryN(0), out xrange, out yrange, out zrange, out mrange);
                    for (var i = 1; i < self.NumGeometries; i++)
                    {
                        Interval x2range, y2range, z2range, m2range;
                        var ring = self.GetGeometryN(i);
                        GetMetric(ring, out x2range, out y2range, out z2range,
                            out m2range);
                        xrange = xrange.ExpandedByInterval(x2range);
                        yrange = yrange.ExpandedByInterval(y2range);
                        zrange = zrange.ExpandedByInterval(z2range);
                        mrange = mrange.ExpandedByInterval(m2range);
                    }
                    break;
            }
        }

        private static void GetMetric(ICoordinateSequence seq, out Interval xrange, out Interval yrange,
            out Interval zrange,
            out Interval mrange)
        {
            xrange = Interval.Create();
            yrange = Interval.Create();
            zrange = Interval.Create();
            mrange = Interval.Create();

            for (var i = 0; i < seq.Count; i++)
            {
                xrange = xrange.ExpandedByValue(seq.GetX(i));
                yrange = yrange.ExpandedByValue(seq.GetY(i));
                if ((seq.Ordinates & Ordinates.Z) == Ordinates.Z)
                    zrange = zrange.ExpandedByValue(seq.GetOrdinate(i, Ordinate.Z));
                if ((seq.Ordinates & Ordinates.M) == Ordinates.M)
                    mrange = mrange.ExpandedByValue(seq.GetOrdinate(i, Ordinate.M));
            }
        }

        private static void GetMetric(IPoint geom, out Interval xrange, out Interval yrange, out Interval zrange,
            out Interval mrange)
        {
            GetMetric(geom.CoordinateSequence, out xrange, out yrange, out zrange, out mrange);
        }
        private static void GetMetric(ILineString geom, out Interval xrange, out Interval yrange, out Interval zrange,
            out Interval mrange)
        {
            GetMetric(geom.CoordinateSequence, out xrange, out yrange, out zrange, out mrange);
        }
        private static void GetMetric(IPolygon geom, out Interval xrange, out Interval yrange, out Interval zrange,
            out Interval mrange)
        {
            GetMetric(geom.Shell.CoordinateSequence, out xrange, out yrange, out zrange, out mrange);
            if (geom.NumInteriorRings > 0)
            {
                for (var i = 0; i < geom.NumInteriorRings; i++)
                {
                    Interval x2range, y2range, z2range, m2range;
                    var ring = geom.GetInteriorRingN(i);
                    GetMetric(ring.CoordinateSequence, out x2range, out y2range, out z2range,
                        out m2range);
                    xrange = xrange.ExpandedByInterval(x2range);
                    yrange = yrange.ExpandedByInterval(y2range);
                    if ((ring.CoordinateSequence.Ordinates & Ordinates.Z) == Ordinates.Z)
                        zrange = zrange.ExpandedByInterval(z2range);
                    if ((ring.CoordinateSequence.Ordinates & Ordinates.M) == Ordinates.M)
                        mrange = mrange.ExpandedByInterval(m2range);

                }


            }
        }
#endif
        internal static void GetMetric(Envelope self,
            out Interval xrange, out Interval yrange,
            out Interval zrange, out Interval mrange)
        {
            xrange = Interval.Create(self.MinX, self.MaxX);
            yrange = Interval.Create(self.MinY, self.MaxY);
            zrange = Interval.Create();
            mrange = Interval.Create();
        }
    }
}