using UnityEngine;

namespace WheatFarm.Core.Data
{
    [CreateAssetMenu(fileName = "Plant_", menuName = "WheatFarm/PlantData")]
    public class PlantData : ScriptableObject
    {
        [Header("Identity")]
        public string PlantId;
        public string DisplayName;
        public PlantCategory Category;

        [Header("Visuals")]
        public Mesh Mesh;
        public Material Material;
        public Vector2 ScaleRange = new(0.8f, 1.2f);

        [Header("Growth")]
        public float GrowthDuration = 60f;
        public bool NeedsWatering = true;

        [Header("Economy")]
        public int SeedCost = 5;
        public int SellPrice = 10;
        public bool RenewableHarvest;

        [Header("Placement")]
        public Vector2Int TrunkSize = Vector2Int.one;

        [Header("Unlock")]
        public bool UnlockedByDefault;
    }
}
