using UnityEngine;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Infrastructure.Save;
using WheatFarm.Inventory;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Grants a small starter pack of seeds on a brand-new game (no save present),
    /// so the plant → water → harvest → sell loop is playable from the first second.
    /// An existing save restores its own inventory, so we skip granting then.
    /// </summary>
    public class StartingInventoryGranter : IStartable
    {
        private const int StarterWheatSeeds = 10;

        private readonly IInventoryService _inventory;
        private readonly IFarmSaveManager _saveManager;

        public StartingInventoryGranter(IInventoryService inventory, IFarmSaveManager saveManager)
        {
            _inventory = inventory;
            _saveManager = saveManager;
        }

        public void Start()
        {
            if (_saveManager.HasSave) return;

            _inventory.TryAdd(new InventoryItem("seed_wheat", ItemType.Seed, StarterWheatSeeds));
            Debug.Log($"[StartingInventory] Granted starter seeds: {StarterWheatSeeds}x wheat");
        }
    }
}
