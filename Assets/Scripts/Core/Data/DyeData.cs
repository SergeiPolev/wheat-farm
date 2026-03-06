using UnityEngine;

namespace WheatFarm.Core.Data
{
    [CreateAssetMenu(menuName = "WheatFarm/DyeData")]
    public class DyeData : ScriptableObject
    {
        public string DyeId;
        public string DisplayName;
        public Color Color = Color.white;
        public int Cost;
        public bool RequiresCrafting;
        public string[] CraftIngredientIds;
    }
}
