using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Player.Tools;

namespace WheatFarm.UI
{
    /// <summary>
    /// Populates CatalogTabBar from PlantDatabase + PlaceableDatabase.
    /// On item click: selects plant/placeable on PlacementTool or equips a tool.
    /// Locked plants are unlocked (for coins) on click via ShopService.
    /// </summary>
    public class CatalogPresenter : IInitializable, IDisposable
    {
        private readonly CatalogTabBar _view;
        private readonly PlantDatabase _plantDb;
        private readonly PlaceableDatabase _placeableDb;
        private readonly IToolService _toolService;
        private readonly PlacementTool _placementTool;
        private readonly IPlantUnlockService _unlock;
        private readonly IShopService _shop;

        private readonly CompositeDisposable _disposables = new();

        // Tab definitions: name -> populate function
        private static readonly string[] TabNames = { "Crops", "Trees", "Buildings", "Decor", "Paths", "Tools" };

        // Current tab's items — can be PlantData or PlaceableData or ToolId
        private readonly List<object> _currentItems = new();
        private int _currentTab;

        public CatalogPresenter(
            CatalogTabBar view,
            PlantDatabase plantDb,
            PlaceableDatabase placeableDb,
            IToolService toolService,
            PlacementTool placementTool,
            IPlantUnlockService unlock,
            IShopService shop)
        {
            _view = view;
            _plantDb = plantDb;
            _placeableDb = placeableDb;
            _toolService = toolService;
            _placementTool = placementTool;
            _unlock = unlock;
            _shop = shop;
        }

        public void Initialize()
        {
            _view.OnTabClicked += OnTabSelected;
            _view.OnItemClicked += OnItemSelected;

            // Refresh lock badges whenever the unlocked set changes
            _unlock.Changed += OnUnlocksChanged;

            // Auto-select Crops tab
            OnTabSelected(0);
        }

        public void Dispose()
        {
            _view.OnTabClicked -= OnTabSelected;
            _view.OnItemClicked -= OnItemSelected;
            _unlock.Changed -= OnUnlocksChanged;
            _disposables.Dispose();
        }

        private void OnUnlocksChanged() => OnTabSelected(_currentTab);

        private void OnTabSelected(int tabIndex)
        {
            _currentTab = tabIndex;
            _view.SetActiveTab(tabIndex);
            _currentItems.Clear();

            var displayItems = new List<(string name, int cost, bool locked)>();

            switch (tabIndex)
            {
                case 0: // Crops
                    PopulatePlants(PlantCategory.Crop, displayItems);
                    break;
                case 1: // Trees (trees + bushes)
                    PopulatePlants(PlantCategory.Tree, displayItems);
                    PopulatePlants(PlantCategory.Bush, displayItems);
                    break;
                case 2: // Buildings
                    PopulatePlaceables(PlaceableCategory.Building, displayItems);
                    break;
                case 3: // Decor
                    PopulatePlaceables(PlaceableCategory.Decor, displayItems);
                    break;
                case 4: // Paths
                    PopulatePlaceables(PlaceableCategory.Path, displayItems);
                    break;
                case 5: // Tools
                    PopulateTools(displayItems);
                    break;
            }

            _view.SetItems(displayItems);
        }

        private void OnItemSelected(int index)
        {
            if (index < 0 || index >= _currentItems.Count) return;

            var item = _currentItems[index];

            if (item is PlantData plant)
            {
                if (!_unlock.IsUnlocked(plant.PlantId))
                {
                    if (!_shop.TryUnlockPlant(plant))
                    {
                        Debug.Log($"[Catalog] {plant.DisplayName} is locked — not enough coins to unlock.");
                        return;
                    }
                    Debug.Log($"[Catalog] Unlocked {plant.DisplayName}.");
                    // OnUnlocksChanged repopulates the tab; the click below still selects it.
                }

                _toolService.EquipTool(ToolId.Placement);
                _placementTool.SelectPlant(plant);
                Debug.Log($"[Catalog] Selected plant: {plant.DisplayName}");
            }
            else if (item is PlaceableData placeable)
            {
                _toolService.EquipTool(ToolId.Placement);
                _placementTool.SelectPlaceable(placeable);
                Debug.Log($"[Catalog] Selected placeable: {placeable.DisplayName}");
            }
            else if (item is ToolId toolId)
            {
                _placementTool.ClearSelection();
                _toolService.EquipTool(toolId);
                Debug.Log($"[Catalog] Equipped tool: {toolId}");
            }
        }

        private void PopulatePlants(PlantCategory category, List<(string, int, bool)> display)
        {
            foreach (var plant in _plantDb.GetByCategory(category))
            {
                _currentItems.Add(plant);
                display.Add((plant.DisplayName, plant.SeedCost, !_unlock.IsUnlocked(plant.PlantId)));
            }
        }

        private void PopulatePlaceables(PlaceableCategory category, List<(string, int, bool)> display)
        {
            foreach (var p in _placeableDb.GetByCategory(category))
            {
                _currentItems.Add(p);
                display.Add((p.DisplayName, p.Cost, !p.UnlockedByDefault));
            }
        }

        private void PopulateTools(List<(string, int, bool)> display)
        {
            var tools = new (ToolId id, string name)[]
            {
                (ToolId.WateringCan, "Watering Can"),
                (ToolId.Sickle, "Sickle"),
                (ToolId.Dye, "Dye"),
                (ToolId.Fertilizer, "Fertilizer"),
                (ToolId.Uproot, "Uproot"),
                (ToolId.Bulldoze, "Bulldoze"),
            };

            foreach (var (id, name) in tools)
            {
                _currentItems.Add(id);
                display.Add((name, 0, false));
            }
        }
    }
}
