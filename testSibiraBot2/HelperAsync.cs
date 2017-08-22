using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace nvk.Helpers
{
    public static class HelperAsync
    {
        async public static Task<TResult> Await<TSource, TResult>(this Task<TSource> @this, Func<TSource, Task<TResult>> fn) => await fn(await @this);
        async public static Task<TResult> Await<TSource, TResult>(this TSource @this, Func<TSource, Task<TResult>> fn) => await fn(@this);
        async public static Task<TResult> Await<TSource, TResult>(this Task<TSource> @this, Func<TSource, TResult> fn) => fn(await @this);
    }
}