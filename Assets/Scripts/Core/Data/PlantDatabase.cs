using UnityEngine;

namespace WheatFarm.Core.Data
{
    [CreateAssetMenu(menuName = "WheatFarm/PlantDatabase")]
    public class PlantDatabase : ScriptableObject
    {
        public PlantData[] Plants;

        public PlantData GetById(string id)
        {
            foreach (var p in Plants)
                if (p.PlantId == id) return p;
            return null;
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
