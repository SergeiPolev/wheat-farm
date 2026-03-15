using System;
using R3;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Farming;
using WheatFarm.Inventory;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Bridges farming events to the economy layer.
    /// Subscribes to PlantSystem.OnHarvested → adds to inventory + contract progress.
    /// </summary>
    public class HarvestRewardHandler : IInitializable, IDisposable
    {
        private readonly IPlantSystem _plantSystem;
        private readonly IInventoryService _inventory;
        private readonly IContractService _contracts;
        private IDisposable _subscription;

        public HarvestRewardHandler(IPlantSystem plantSystem, IInventoryService inventory, IContractService contracts)
        {
            _plantSystem = plantSystem;
            _inventory = inventory;
            _contracts = contracts;
        }

        public void Initialize()
        {
            _subscription = _plantSystem.OnHarvested.Subscribe(OnHarvested);
        }

        private void OnHarvested(HarvestData data)
        {
            var item = new InventoryItem(data.PlantId, ItemType.Harvest, 1);
            _inventory.TryAdd(item);
            _contracts.ContributeItem(data.PlantId, 1);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
