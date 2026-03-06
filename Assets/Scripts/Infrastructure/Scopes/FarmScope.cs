using VContainer;
using VContainer.Unity;
using UnityEngine;
using WheatFarm.Buildings;
using WheatFarm.Farming;
using WheatFarm.Infrastructure.Save;
using WheatFarm.Player;
using WheatFarm.Player.Tools;
using WheatFarm.UI;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Farm lifetime scope — child of GameScope.
    /// Registers farming systems: chunks, plants, brush, buildings, tools, UI.
    /// </summary>
    public class FarmScope : LifetimeScope
    {
        [SerializeField] private FarmRenderConfig _renderConfig;

        [Header("Player (assign when Player GO is set up)")]
        [SerializeField] private FarmInteractionController _interactionController;

        [Header("UI Views (assign when Canvas is set up)")]
        [SerializeField] private HUDView _hudView;
        [SerializeField] private ShopView _shopView;
        [SerializeField] private InventoryView _inventoryView;
        [SerializeField] private ContractBoardView _contractBoardView;

        protected override void Configure(IContainerBuilder builder)
        {
            Debug.Log("[FarmScope] Configure");

            // Config instance (assigned in Inspector)
            builder.RegisterInstance(_renderConfig);

            // Phase 3: Chunk-based farming core
            builder.Register<ChunkSystem>(Lifetime.Singleton)
                .WithParameter<float>(_renderConfig.ChunkWorldSize)
                .WithParameter<int>(_renderConfig.SubCellResolution)
                .As<IChunkSystem>();

            builder.Register<PlantSystem>(Lifetime.Singleton)
                .As<IPlantSystem, ITickable>();

            builder.Register<BrushService>(Lifetime.Singleton)
                .As<IBrushService>();

            builder.Register<FarmRenderSystem>(Lifetime.Singleton)
                .As<ITickable, System.IDisposable>();

            builder.Register<FarmBootstrap>(Lifetime.Singleton)
                .As<IStartable>();

            // Phase 4: Tools
            builder.Register<PlanterTool>(Lifetime.Singleton).As<PlanterTool, ITool>();
            builder.Register<WateringCanTool>(Lifetime.Singleton).As<ITool>();
            builder.Register<SickleTool>(Lifetime.Singleton).As<ITool>();
            builder.Register<DyeTool>(Lifetime.Singleton).As<ITool>();
            builder.Register<FertilizerTool>(Lifetime.Singleton).As<ITool>();
            builder.Register<UprootTool>(Lifetime.Singleton).As<ITool>();

            builder.Register<ToolService>(Lifetime.Singleton)
                .As<IToolService>();

            // Auto-select first unlocked plant on start
            builder.Register<PlantAutoSelector>(Lifetime.Singleton)
                .As<IStartable>();

            // Phase 6: Buildings & Production
            builder.Register<BuildingService>(Lifetime.Singleton)
                .As<IBuildingService>();

            builder.Register<ProductionService>(Lifetime.Singleton)
                .As<IProductionService, ITickable>();

            // Phase 7: Tree placement
            builder.Register<TreePlacementService>(Lifetime.Singleton)
                .As<ITreePlacementService>();

            // Phase 10: Save/Load manager
            builder.Register<FarmSaveManager>(Lifetime.Singleton)
                .As<IFarmSaveManager>();

            // Player interaction (optional — assign in Inspector when Player GO exists)
            if (_interactionController != null)
            {
                builder.RegisterComponent(_interactionController);
            }

            // Phase 9: UI (MVP) — Views are optional; Presenters only created when View is assigned
            RegisterUI(builder);
        }

        private void RegisterUI(IContainerBuilder builder)
        {
            if (_hudView != null)
            {
                builder.RegisterComponent(_hudView);
                builder.Register<HUDPresenter>(Lifetime.Singleton)
                    .As<IInitializable, System.IDisposable>();
            }

            if (_shopView != null)
            {
                builder.RegisterComponent(_shopView);
                builder.Register<ShopPresenter>(Lifetime.Singleton)
                    .As<IInitializable, System.IDisposable>();
            }

            if (_inventoryView != null)
            {
                builder.RegisterComponent(_inventoryView);
                builder.Register<InventoryPresenter>(Lifetime.Singleton)
                    .As<IInitializable, System.IDisposable>();
            }

            if (_contractBoardView != null)
            {
                builder.RegisterComponent(_contractBoardView);
                builder.Register<ContractBoardPresenter>(Lifetime.Singleton)
                    .As<IInitializable, System.IDisposable>();
            }
        }
    }
}
