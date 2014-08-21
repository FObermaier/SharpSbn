using System;
#if UseGeoAPI
using Interval = GeoAPI.DataStructures.Interval;
using Envelope = GeoAPI.Geometries.Envelope;
#else
using Interval = SharpSbn.DataStructures.Interval;
using Envelope = SharpSbn.DataStructures.Envelope;
#endif

namespace SharpSbn
{
    /// <summary>
    /// Utility class to clamp a value to a byte
    /// </summary>
    internal static class ClampUtility
    {
        internal static byte ScaleLower(double value, Interval range)
        {
            var min = ((value - range.Min) / range.Width * 255.0);
            // not sure why this rounding is needed, but it is
            var modMin = (min % 1 - .005) % 1 + (int)min;
            var res = (int)(Math.Floor(modMin));
            if (res < 0) res = 0;
            return (byte)res;
        }

        internal static byte ScaleUpper(double value, Interval range)
        {
            var max = ((value - range.Min) / range.Width * 255.0);
            var modMax = (max % 1 + .005) % 1 + (int)max;
            var res = (int)Math.Ceiling(modMax);
            if (res > 255) res = 255;
            return (byte)res;
        }

        internal static void Clamp(Envelope scale, Envelope envelope, 
                                   out byte minx, out byte miny, out byte maxx, out byte maxy)
        {
            var xrange = Interval.Create(scale.MinX, scale.MaxX);
            var yrange = Interval.Create(scale.MinY, scale.MaxY);

            minx = ScaleLower(envelope.MinX, xrange);
            maxx = ScaleUpper(envelope.MaxX, xrange);
            miny = ScaleLower(envelope.MinY, yrange);
            maxy = ScaleUpper(envelope.MaxY, yrange);
        }
    }
}