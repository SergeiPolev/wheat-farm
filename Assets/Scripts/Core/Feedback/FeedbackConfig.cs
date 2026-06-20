using UnityEngine;

namespace WheatFarm.Core
{
    /// <summary>
    /// Maps each FarmFxType to a ParticleSystem prefab. Indexed by enum order:
    /// Plant, Water, Harvest, Uproot, Build, Remove. Missing entries fall back to a
    /// simple procedural burst in FeedbackService.
    /// </summary>
    [CreateAssetMenu(menuName = "WheatFarm/FeedbackConfig")]
    public class FeedbackConfig : ScriptableObject
    {
        public GameObject[] EffectPrefabs;

        public GameObject GetPrefab(FarmFxType type)
        {
            int i = (int)type;
            if (EffectPrefabs == null || i < 0 || i >= EffectPrefabs.Length) return null;
            return EffectPrefabs[i];
        }
    }
}
