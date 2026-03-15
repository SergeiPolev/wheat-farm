using VContainer;
using VContainer.Unity;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.DayNight;
using WheatFarm.Economy;
using WheatFarm.Inventory;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Game session scope — child of RootScope.
    /// Registers economy, input, camera, day/night services.
    /// </summary>
    public class GameScope : LifetimeScope
    {
        [SerializeField] private PlantDatabase _plantDatabase;
        [SerializeField] private BuildingDatabase _buildingDatabase;
        [SerializeField] private PlaceableDatabase _placeableDatabase;
        [SerializeField] private ContractDatabase _contractDatabase;

        protected override void Configure(IContainerBuilder builder)
        {
            Debug.Log("[GameScope] Configure");

            // Data: databases (available to all child scopes)
            builder.RegisterInstance(_plantDatabase);

            if (_buildingDatabase != null)
                builder.RegisterInstance(_buildingDatabase);

            if (_placeableDatabase != null)
                builder.RegisterInstance(_placeableDatabase);

            if (_contractDatabase != null)
                builder.RegisterInstance(_contractDatabase);

            // Phase 5: Economy
            builder.Register<WalletService>(Lifetime.Singleton)
                .As<IWalletService>();

            builder.Register<InventoryService>(Lifetime.Singleton)
                .As<IInventoryService>();

            builder.Register<ShopService>(Lifetime.Singleton)
                .As<IShopService>();

            builder.Register<ContractService>(Lifetime.Singleton)
                .As<IContractService>();

            // Phase 8: Day/Night cycle
            builder.Register<DayNightService>(Lifetime.Singleton)
                .As<IDayNightService, ITickable>();

            // TODO: InputService, CameraService
        }
    }
}
