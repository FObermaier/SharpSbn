using System.Collections.Generic;

namespace SharpSbn
{
    internal static class NumPySlicing
    {
        internal static IList<T> GetRange<T>(IList<T> self, int start, int end, int step = 1)
        {
            List<T> res = null;
            if (step < 0)
            {
                res = (List<T>)GetRange<T>(self, end, start, -step);
                res.Reverse();
                return res;
            }

            if (end < start) 
                return new List<T>(0);

            var size = (end - start + 1)/step;

            res = new List<T>(size);
            var i = start;
            while (i <= end)
            {
                res.Add(self[i]);
                i += step;
            }
            return res;
        }
    }
}