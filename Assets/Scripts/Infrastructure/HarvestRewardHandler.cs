using System;
using R3;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Farming;
using WheatFarm.Inventory;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Bridges farming events to the economy layer.
    /// Subscribes to PlantSystem.OnHarvested and adds harvest items to inventory.
    /// </summary>
    public class HarvestRewardHandler : IInitializable, IDisposable
    {
        private readonly IPlantSystem _plantSystem;
        private readonly IInventoryService _inventory;
        private IDisposable _subscription;

        public HarvestRewardHandler(IPlantSystem plantSystem, IInventoryService inventory)
        {
            _plantSystem = plantSystem;
            _inventory = inventory;
        }

        public void Initialize()
        {
            _subscription = _plantSystem.OnHarvested.Subscribe(OnHarvested);
        }

        private void OnHarvested(HarvestData data)
        {
            // Add harvested crop to inventory (e.g. "wheat" x1)
            var item = new InventoryItem(data.PlantId, ItemType.Harvest, 1);
            bool added = _inventory.TryAdd(item);
            UnityEngine.Debug.Log($"[Harvest] {data.PlantId} x1 (sell={data.Yield}) → inventory {(added ? "OK" : "FULL")} | items: {_inventory.Items.Count}, slots: {_inventory.UsedSlots}/{_inventory.Capacity.CurrentValue}");
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
