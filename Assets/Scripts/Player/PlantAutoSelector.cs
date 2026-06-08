using UnityEngine;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Player.Tools;

namespace WheatFarm.Player
{
    /// <summary>
    /// Auto-selects the first unlocked plant on farm start so the PlacementTool works immediately.
    /// Lives in Player assembly to avoid circular dependency (Farming cannot reference Player).
    /// </summary>
    public class PlantAutoSelector : IStartable
    {
        private readonly IPlantUnlockService _unlock;
        private readonly PlacementTool _placementTool;

        public PlantAutoSelector(IPlantUnlockService unlock, PlacementTool placementTool)
        {
            _unlock = unlock;
            _placementTool = placementTool;
        }

        public void Start()
        {
            var unlocked = _unlock.GetUnlocked();
            if (unlocked.Length > 0)
            {
                _placementTool.SelectPlant(unlocked[0]);
                Debug.Log($"[PlantAutoSelector] Default plant: {unlocked[0].PlantId}");
            }
            else
            {
                Debug.LogWarning("[PlantAutoSelector] No unlocked plants!");
            }
        }
    }
}
