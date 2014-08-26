using System;
using System.Collections.Generic;


// ReSharper disable once CheckNamespace
namespace FrameworkReplacements
{
#if !(NET40 || NET45)
    public class Tuple<T1, T2>
    {
        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }
    }

    public static class Tuple
    {
        public static Tuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
        {
            return new Tuple<T1, T2> {Item1 = item1, Item2 = item2};
        }
    }
#endif

    namespace Linq
    {
        internal static class Enumerable
        {
#if !(NET40 || NET45)
            public static IEnumerable<T> Skip<T>(IEnumerable<T> items, int count)
            {
                var i = 0;
                foreach (var item in items)
                {
                    if (i >= count)
                        yield return item;
                    i++;
                }
            }
#endif
            internal static IEnumerable<T> GetRange<T>(IList<T> list, int start, int count)
            {
                for (var i = 0; i < count; i++)
                    yield return list[start + i];
            }

            internal static T[] GetRange<T>(T[] list, int start, int count)
            {
                var res = new T[count];
                Array.Copy(list, start, res, 0, count);
                return res;
            }
        }
    }
}

