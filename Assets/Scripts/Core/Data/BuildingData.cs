using UnityEngine;

namespace WheatFarm.Core.Data
{
    public enum BuildingType
    {
        Functional,
        Decorative
    }

    [CreateAssetMenu(menuName = "WheatFarm/BuildingData")]
    public class BuildingData : ScriptableObject
    {
        [Header("Identity")]
        public string BuildingId;
        public string DisplayName;
        public BuildingType Type;

        [Header("Placement")]
        public Vector2Int GridSize = new(2, 2);
        public GameObject Prefab;

        [Header("Economy")]
        public int Cost;
        public int PlanksRequired;

        [Header("Production")]
        public int MaxLevel = 3;
        public RecipeData[] Recipes;

        [Header("Unlock")]
        public bool StarterBuilding;
    }
}
