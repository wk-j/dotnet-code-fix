using System.Collections.Immutable;
using System.Collections.Generic;

namespace DotNetCodeFix {
    static class Extensions {
        internal static void AddToInnerList<TKey, TValue>(this IDictionary<TKey, ImmutableList<TValue>> dictionary, TKey key, TValue item) {
            ImmutableList<TValue> items;

            if (dictionary.TryGetValue(key, out items)) {
                dictionary[key] = items.Add(item);
            } else {
                dictionary.Add(key, ImmutableList.Create(item));
            }
        }
    }
}
