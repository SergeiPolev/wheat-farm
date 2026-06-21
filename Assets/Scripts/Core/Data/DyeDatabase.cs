using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Core.Data
{
    /// <summary>
    /// Catalog of all dyes available in the palette (mirrors <see cref="PlantDatabase"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "WheatFarm/DyeDatabase")]
    public class DyeDatabase : ScriptableObject
    {
        public DyeData[] Items;

        private Dictionary<string, DyeData> _cache;

        public IReadOnlyList<DyeData> All => Items;

        public DyeData GetById(string id)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<string, DyeData>();
                foreach (var d in Items)
                    if (d != null) _cache[d.DyeId] = d;
            }
            return _cache.GetValueOrDefault(id);
        }
    }
}
