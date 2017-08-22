using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace nvk.Helpers
{
    public static class HelperLinq
    {
        public static TAccumulate AggregateWithDelimiter<TSource, TAccumulate>(this IEnumerable<TSource> source,
            TAccumulate seed, Func<TAccumulate, TAccumulate> delimiter, Func<TAccumulate, TSource, TAccumulate> func)
        {
            if ((null == source) || (null == func)) { return seed; }

            var acc = seed;
            var isFirst = true;
            foreach (TSource e in source)
            {
                if (true == isFirst) { isFirst = false; }
                else { acc = delimiter(acc); }

                acc = func(acc, e); 
            }

            return acc;
        }

    }
}