﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Numerics;

namespace System.Collections.Generic
{
    public static class CollectionExtensions
    {
        public static bool Empty<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> collection)
        {
            return collection.Count == 0;
        }

        public static bool Empty<TValue>(this IReadOnlyList<TValue> collection)
        {
            return collection.Count == 0;
        }

        public static bool Empty<TValue>(this IReadOnlySet<TValue> collection)
        {
            return collection.Count == 0;
        }

        /// <summary>
        /// Returns the entry in this list at the given index, or the default value of the element
        /// type if the index was out of bounds.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the list.</typeparam>
        /// <param name="list">The list to retrieve from.</param>
        /// <param name="index">The index to try to retrieve at.</param>
        /// <returns>The value, or the default value of the element type.</returns>
        public static T LookupByIndex<T>(this IList<T> list, int index)
        {
            return index >= list.Count ? default : list[index];
        }

        /// <summary>
        /// Returns the entry in this dictionary at the given key, or the default value of the key
        /// if none.
        /// </summary>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="dict">The dictionary to operate on.</param>
        /// <param name="key">The key of the element to retrieve.</param>
        /// <returns>The value (if any).</returns>
        //public static TValue LookupByKey<TKey, TValue>(this IDictionary<TKey, TValue> dict, object key)
        //{
        //    TValue val;
        //    TKey newkey = (TKey)Convert.ChangeType(key, typeof(TKey));
        //    return dict.TryGetValue(newkey, out val) ? val : default;
        //}

        public static TValue LookupByKey<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            TValue val;
            return dict.TryGetValue(key, out val) ? val : default;
        }

        public static KeyValuePair<TKey, TValue> Find<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            if (!dict.ContainsKey(key))
                return default;

            return new KeyValuePair<TKey, TValue>(key, dict[key]);
        }

        public static TValue? Find<TValue>(this IReadOnlyList<TValue> list, Predicate<TValue> match)
        {
            if (match == null)
            {
                throw new ArgumentNullException();
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (match(list[i]))
                {
                    return list[i];
                }
            }

            return default;
        }

        public static bool Contains<TValue>(this IReadOnlyList<TValue> list, TValue value) where TValue : IBinaryNumber<TValue>
        {
            return list.TryFind(out _, out _, (item) => item == value);
        }

        public static bool Contains(this IReadOnlyList<object> list, object value)
        {
            return list.TryFind(out _, out _, (item) => item.Equals(value));
        }

        public static bool TryFind<TValue>(this IReadOnlyList<TValue> list, out TValue value, out int index, Predicate<TValue> match, bool aback = false)
        {
            value = default;
            index = -1;

            if (match == null)
            {
                throw new ArgumentNullException();
            }

            if (list.Count == 0)
                return false;

            int start = aback ? list.Count - 1 : 0;
            int end = aback ? -1 : list.Count;
            int iter = start;

            while(iter != end)
            {
                if (match(list[iter]))
                {
                    value = list[iter];
                    index = iter;
                    return true;
                }

                iter = aback ? --iter : ++iter;
            }

            return false;
        }

        //public static bool ContainsKey<TKey, TValue>(this IDictionary<TKey, TValue> dict, object key)
        //{
        //    TKey newkey = (TKey)Convert.ChangeType(key, typeof(TKey));
        //    return dict.ContainsKey(newkey);
        //}

        public static void RemoveAll<T>(this List<T> collection, ICheck<T> check)
        {
            collection.RemoveAll(check.Invoke);
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.OrderBy(x => Guid.NewGuid());
        }

        public static void Swap<T>(this T[] array, int position1, int position2)
        {
            //
            // Swaps elements in an array. Doesn't need to return a reference.
            //
            T temp = array[position1]; // Copy the first position's element
            array[position1] = array[position2]; // Assign to the second element
            array[position2] = temp; // Assign to the first element
        }

        public static void Resize<T>(this List<T> list, int size)
        {
            int cur = list.Count;
            if (size < cur)
                list.RemoveRange(size, cur - size);
            else
            {
                for (var i = list.Count; i < size; ++i)
                    list.Add(default);
            }
        }

        public static void RandomResize<T>(this IList<T> list, int size)
        {
            int listSize = list.Count;

            while (listSize > size)
            {
                list.RemoveAt(RandomHelper.IRand(0, listSize));
                --listSize;
            }
        }

        public static void RandomResize<T>(this List<T> list, Predicate<T> predicate, int size)
        {
            for (var i = 0; i < list.Count; ++i)
            {
                var obj = list[i];
                if (!predicate(obj))
                    list.Remove(obj);
            }

            if (size != 0)
                list.Resize(size);
        }

        public static void RandomShuffle<T>(this IList<T> array)
        {
            for (int n = array.Count; n > 1;)
            {
                int k = RandomHelper.Rand32(n);
                --n;
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }

        public static void RandomShuffle<T>(this IList<T> array, int first, int count)
        {
            for (int n = count; n > 1;)
            {
                int k = RandomHelper.Rand32(n);
                --n;
                T temp = array[n + first];
                array[n + first] = array[k + first];
                array[k + first] = temp;
            }
        }

        public static T SelectRandom<T>(this IEnumerable<T> source)
        {
            return source.SelectRandom(1).Single();
        }

        public static IEnumerable<T> SelectRandom<T>(this IEnumerable<T> source, int count)
        {
            return source.Shuffle().Take(count);
        }

        public static T SelectRandomElementByWeight<T>(this IEnumerable<T> sequence, Func<T, float> weightSelector)
        {
            float totalWeight = sequence.Sum(weightSelector);
            // The weight we are after...
            float itemWeightIndex = RandomHelper.NextSingle() * totalWeight;
            float currentWeightIndex = 0;

            foreach (var item in from weightedItem in sequence select new { Value = weightedItem, Weight = weightSelector(weightedItem) })
            {
                currentWeightIndex += item.Weight;

                // If we've hit or passed the weight we are after for this item then it's the one we want....
                if (currentWeightIndex >= itemWeightIndex)
                    return item.Value;

            }

            return default;
        }

        public static IEnumerable<TSource> Intersect<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second, Func<TSource, TSource, bool> comparer)
        {
            return first.Where(x => second.Count(y => comparer(x, y)) == 1);
        }

        public static int[] ToBlockRange(this BitSet array)
        {
            int[] blockValues = new int[array.Length / 32 + 1];
            array.CopyTo(blockValues, 0);
            return blockValues;
        }

        public static void Clear(this Array array)
        {
            Array.Clear(array, 0, array.Length);
        }

        public static void EnsureWritableListIndex<T>(this List<T> list, int index, T defaultValue)
        {
            while (list.Count <= index)
                list.Add(defaultValue);
        }
    }

    public interface ICheck<in T>
    {
        bool Invoke(T obj);
    }

    public interface IDoWork<in T>
    {
        void Invoke(T obj);
    }
}
