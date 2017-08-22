using System;
using System.Collections.Generic;

namespace nvk.Helpers
{
    public static class HelperCollections
    {
        public static (bool isFound, TValue val) Find<TKey, TValue>(this Dictionary<TKey, TValue> src, TKey key)
        {
            if (null == src) { return (false, default(TValue)); }

            return (src.TryGetValue(key, out var val), val);
        }        
    }
}