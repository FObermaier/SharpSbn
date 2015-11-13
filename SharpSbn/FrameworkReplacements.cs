using System;
using System.Collections.Generic;


// ReSharper disable once CheckNamespace
namespace FrameworkReplacements
{
#if !(NET40 || NET45)
    /// <summary>
    /// A framework replacement for System.Tuple&lt;T1, T2&gt;.
    /// </summary>
    /// <typeparam name="T1">The type of the first item</typeparam>
    /// <typeparam name="T2">The type of the second item</typeparam>
    public class Tuple<T1, T2>
    {
        /// <summary>
        /// Gets or sets a value indicating the first item
        /// </summary>
        public T1 Item1 { get; set; }
        /// <summary>
        /// Gets or sets a value indicating the second item
        /// </summary>
        public T2 Item2 { get; set; }
    }

    /// <summary>
    /// A utility class to create <see cref="Tuple{T1, T2}"/> items.
    /// </summary>
    public static class Tuple
    {
        /// <summary>
        /// A Factory method to create <see cref="Tuple{T1, T2}"/> items.
        /// </summary>
        /// <typeparam name="T1">The type of the first item</typeparam>
        /// <typeparam name="T2">The type of the second item</typeparam>
        /// <param name="item1">The first item</param>
        /// <param name="item2">The second item</param>
        /// <returns></returns>
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

