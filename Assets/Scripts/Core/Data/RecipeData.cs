using UnityEngine;

namespace WheatFarm.Core.Data
{
    [CreateAssetMenu(menuName = "WheatFarm/RecipeData")]
    public class RecipeData : ScriptableObject
    {
        public string RecipeId;
        public string DisplayName;
        public ItemStack[] Inputs;
        public ItemStack Output;
        public float ProcessingTime = 60f;
    }
}
