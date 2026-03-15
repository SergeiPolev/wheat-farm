using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Core.Data
{
    [CreateAssetMenu(menuName = "WheatFarm/PlaceableDatabase")]
    public class PlaceableDatabase : ScriptableObject
    {
        public PlaceableData[] Items;

        private Dictionary<string, PlaceableData> _cache;

        public PlaceableData GetById(string id)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<string, PlaceableData>();
                foreach (var item in Items)
                    if (item != null) _cache[item.PlaceableId] = item;
            }
            return _cache.GetValueOrDefault(id);
        }

        public List<PlaceableData> GetByCategory(PlaceableCategory category)
        {
            var result = new List<PlaceableData>();
            foreach (var item in Items)
                if (item != null && item.Category == category)
                    result.Add(item);
            return result;
        }
    }
}
