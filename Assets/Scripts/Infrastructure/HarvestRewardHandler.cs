using System;
using R3;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Farming;
using WheatFarm.Inventory;
using WheatFarm.Player.Charges;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Bridges farming events to the economy layer.
    /// Subscribes to PlantSystem.OnHarvested → adds to inventory.
    /// Contract progress is inventory-derived; no direct contribution here.
    /// </summary>
    public class HarvestRewardHandler : IInitializable, IDisposable
    {
        private readonly IPlantSystem _plantSystem;
        private readonly IInventoryService _inventory;
        private readonly IToolChargeService _charges;
        private IDisposable _subscription;

        public HarvestRewardHandler(IPlantSystem plantSystem, IInventoryService inventory, IToolChargeService charges = null)
        {
            _plantSystem = plantSystem;
            _inventory = inventory;
            _charges = charges;
        }

        public void Initialize()
        {
            _subscription = _plantSystem.OnHarvested.Subscribe(OnHarvested);
        }

        private void OnHarvested(HarvestData data)
        {
            // Basket loop: crops ride in the basket until unloaded at the Warehouse.
            string bonus = data.SeedYield > 0 ? $"seed_{data.PlantId}" : null;
            if (_charges != null && _charges.TryStoreHarvest(data.PlantId, bonus, data.SeedYield))
                return;

            var item = new InventoryItem(data.PlantId, ItemType.Harvest, 1);
            _inventory.TryAdd(item);

            // Return seeds so farming is self-sustaining (buying is for expansion)
            if (data.SeedYield > 0)
                _inventory.TryAdd(new InventoryItem($"seed_{data.PlantId}", ItemType.Seed, data.SeedYield));
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
