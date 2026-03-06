using UnityEngine;

namespace WheatFarm.Core.Data
{
    [CreateAssetMenu(menuName = "WheatFarm/BuildingDatabase")]
    public class BuildingDatabase : ScriptableObject
    {
        public BuildingData[] Buildings;

        public BuildingData GetById(string id)
        {
            foreach (var b in Buildings)
                if (b.BuildingId == id) return b;
            return null;
        }

        public BuildingData[] GetStarter()
        {
            return System.Array.FindAll(Buildings, b => b.StarterBuilding);
        }
    }
}
