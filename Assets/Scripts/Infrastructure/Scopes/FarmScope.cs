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
        [SerializeField] private PlayerController _playerController;

        [Header("Lighting")]
        [SerializeField] private Light _directionalLight;

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
                .As<IChunkSystem, System.IDisposable>();

            builder.Register<PlantSystem>(Lifetime.Singleton)
                .As<IPlantSystem, ITickable, System.IDisposable>();

            builder.Register<BrushService>(Lifetime.Singleton)
                .As<IBrushService, System.IDisposable>();

            builder.Register<FarmRenderSystem>(Lifetime.Singleton)
                .As<ITickable, System.IDisposable>();

            builder.Register<FarmBootstrap>(Lifetime.Singleton)
                .As<IStartable>();

            // Phase 5: Harvest → Inventory + Contracts bridge
            builder.Register<HarvestRewardHandler>(Lifetime.Singleton)
                .As<IInitializable, System.IDisposable>();

            builder.Register<ContractStarter>(Lifetime.Singleton)
                .As<IStartable>();

            // Phase 4: Tools
            builder.Register<PlacementTool>(Lifetime.Singleton).As<PlacementTool, ITool>();
            builder.Register<WateringCanTool>(Lifetime.Singleton).As<ITool>();
            builder.Register<SickleTool>(Lifetime.Singleton).As<ITool>();
            builder.Register<DyeTool>(Lifetime.Singleton).As<ITool>();
            builder.Register<FertilizerTool>(Lifetime.Singleton).As<ITool>();
            builder.Register<UprootTool>(Lifetime.Singleton).As<ITool>();
            builder.Register<BulldozeTool>(Lifetime.Singleton).As<ITool>();

            builder.Register<ToolService>(Lifetime.Singleton)
                .As<IToolService, System.IDisposable>();

            // Auto-select first unlocked plant on start
            builder.Register<PlantAutoSelector>(Lifetime.Singleton)
                .As<IStartable>();

            // Phase 6: Buildings & Production
            builder.Register<PlacementService>(Lifetime.Singleton)
                .As<IPlacementService>();

            builder.Register<ProductionService>(Lifetime.Singleton)
                .As<IProductionService, ITickable, System.IDisposable>();

            // Phase 7: Tree placement
            builder.Register<TreePlacementService>(Lifetime.Singleton)
                .As<ITreePlacementService>();

            // Phase 8: Lighting controller (drives Directional Light from DayNightService)
            if (_directionalLight == null)
                _directionalLight = FindFirstObjectByType<Light>();
            if (_directionalLight != null)
            {
                builder.RegisterInstance(_directionalLight);
                builder.Register<DayNight.LightingController>(Lifetime.Singleton)
                    .As<ITickable>();
            }

            // Phase 10: Save/Load
            builder.Register<FarmSaveManager>(Lifetime.Singleton)
                .As<IFarmSaveManager>();

            builder.Register<Save.SaveLoadController>(Lifetime.Singleton)
                .As<IStartable, ITickable>();

            // Player interaction (optional — assign in Inspector when Player GO exists)
            if (_interactionController != null)
            {
                builder.RegisterComponent(_interactionController);
            }

            // Player controller (needs IToolService to decide facing direction)
            if (_playerController != null)
            {
                builder.RegisterComponent(_playerController);
            }

            // Phase 9: UI (MVP) — Views are optional; Presenters only created when View is assigned
            RegisterUI(builder);
        }

        private void RegisterUI(IContainerBuilder builder)
        {
            // Build HUD programmatically if not assigned in Inspector
            if (_hudView == null)
            {
                var builderGo = new UnityEngine.GameObject("HUDBuilder");
                var hudBuilder = builderGo.AddComponent<HUDBuilder>();
                _hudView = hudBuilder.BuiltHUDView;
                // Builder GO can stay — it's just a container, Canvas is separate
            }

            if (_hudView != null)
            {
                builder.RegisterComponent(_hudView);
                builder.Register<HUDPresenter>(Lifetime.Singleton)
                    .As<IInitializable, System.IDisposable>();
            }

            // Build Shop/Inventory panels programmatically if not assigned
            var canvasRoot = _hudView != null ? _hudView.transform.parent : null;
            if (_shopView == null && canvasRoot != null)
                _shopView = PanelBuilder.BuildShopPanel(canvasRoot);
            if (_inventoryView == null && canvasRoot != null)
                _inventoryView = PanelBuilder.BuildInventoryPanel(canvasRoot);

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

            // Build contract panel if not assigned
            if (_contractBoardView == null && canvasRoot != null)
                _contractBoardView = PanelBuilder.BuildContractPanel(canvasRoot);

            if (_contractBoardView != null)
            {
                builder.RegisterComponent(_contractBoardView);
                builder.Register<ContractBoardPresenter>(Lifetime.Singleton)
                    .As<IInitializable, System.IDisposable>();
            }

            // Build building panel programmatically
            if (canvasRoot != null)
            {
                var buildingPanel = PanelBuilder.BuildBuildingPanel(canvasRoot);
                if (buildingPanel != null)
                {
                    builder.RegisterComponent(buildingPanel);
                    builder.Register<BuildingPanelPresenter>(Lifetime.Singleton)
                        .As<IInitializable, ITickable, System.IDisposable>();
                }
            }

            // Catalog tab bar (bottom of screen — category selection)
            if (canvasRoot != null)
            {
                var catalogGo = new UnityEngine.GameObject("CatalogTabBarHost");
                var catalogTabBar = catalogGo.AddComponent<CatalogTabBar>();
                catalogTabBar.Build(canvasRoot, new[] { "Crops", "Trees", "Buildings", "Decor", "Paths", "Tools" });

                builder.RegisterComponent(catalogTabBar);
                builder.Register<CatalogPresenter>(Lifetime.Singleton)
                    .As<IInitializable, System.IDisposable>();
            }

            // Keybinds for panel toggling (Tab=Shop, I=Inventory, C=Contracts)
            {
                var toggleGo = new UnityEngine.GameObject("UIToggleController");
                var toggle = toggleGo.AddComponent<UIToggleController>();
                toggle.Init(_shopView, _inventoryView, _contractBoardView);
            }
        }
    }
}
