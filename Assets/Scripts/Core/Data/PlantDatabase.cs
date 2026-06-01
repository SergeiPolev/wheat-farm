using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Core.Data
{
    [CreateAssetMenu(menuName = "WheatFarm/PlantDatabase")]
    public class PlantDatabase : ScriptableObject
    {
        public PlantData[] Plants;

        private Dictionary<string, PlantData> _cache;

        public PlantData GetById(string id)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<string, PlantData>();
                foreach (var p in Plants)
                    if (p != null) _cache[p.PlantId] = p;
            }
            return _cache.GetValueOrDefault(id);
        }

        public PlantData[] GetByCategory(PlantCategory category)
        {
            return System.Array.FindAll(Plants, p => p.Category == category);
        }

        public PlantData[] GetUnlocked()
        {
            return System.Array.FindAll(Plants, p => p.UnlockedByDefault);
        }
    }
}
