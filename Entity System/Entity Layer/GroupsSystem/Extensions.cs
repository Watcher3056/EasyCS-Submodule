using System.Collections.Generic;
using System.Linq;

namespace EasyCS.Groups
{
    public static partial class Extensions
    {
        public static Dictionary<T, int> GetCommonElementsMap<T>(params IEnumerable<T>[] lists)
        {
            Dictionary<T, int> map = new Dictionary<T, int>();

            foreach (IEnumerable<T> list in lists)
            {
                foreach (T item in list)
                {
                    // Item encountered, increment count
                    int currCount;
                    if (!map.TryGetValue(item, out currCount))
                        currCount = 0;

                    currCount++;
                    map[item] = currCount;
                }
            }

            return map;
        }

        public static List<T> GetCommonElementsFromMap<T>(Dictionary<T, int> map, int listCount)
        {
            List<T> result = new List<T>();
            foreach (KeyValuePair<T, int> kvp in map)
            {
                // Items whose occurrence count is equal to the number of lists are common to all the lists
                if (kvp.Value == listCount)
                    result.Add(kvp.Key);
            }

            return result;
        }

        public static List<T> FindCommon<T>(params IEnumerable<T>[] lists)
        {
            Dictionary<T, int> map = GetCommonElementsMap(lists);
            int listCount = lists.Count();

            List<T> result = GetCommonElementsFromMap(map, listCount);

            return result;
        }
    }
}