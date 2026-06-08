using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Inventory;
using WheatFarm.Player;

namespace WheatFarm.UI
{
    /// <summary>
    /// Building panel presenter — opens panel when player clicks an interactable building,
    /// drives recipe list, active slots, upgrade, and auto-repeat.
    /// </summary>
    public class BuildingPanelPresenter : IInitializable, IDisposable, ITickable
    {
        private const int UpgradeCoinCostPerLevel = 100;
        private const int UpgradePlankCostPerLevel = 5;
        private const string PlankItemId = "planks";

        private readonly BuildingPanelView _view;
        private readonly IProductionService _production;
        private readonly IInventoryService _inventory;
        private readonly IWalletService _wallet;
        private readonly FarmInteractionController _interaction;
        private readonly IPlacementService _placement;
        private readonly CompositeDisposable _disposables = new();

        private PlacedObject _currentBuilding;

        public BuildingPanelPresenter(
            BuildingPanelView view,
            IProductionService production,
            IInventoryService inventory,
            IWalletService wallet,
            FarmInteractionController interaction,
            IPlacementService placement)
        {
            _view = view;
            _production = production;
            _inventory = inventory;
            _wallet = wallet;
            _interaction = interaction;
            _placement = placement;
        }

        public void Initialize()
        {
            _interaction.OnBuildingClicked += OnBuildingClicked;
            _view.OnStartRecipeClicked += OnStartRecipe;
            _view.OnStopSlotClicked += OnStopSlot;
            _view.OnUpgradeClicked += OnUpgrade;
            _view.OnCloseClicked += OnClose;
            _view.OnAutoRepeatToggled += OnAutoRepeatToggled;

            // React to slot changes to refresh UI
            _production.OnSlotsChanged
                .Subscribe(building =>
                {
                    if (_currentBuilding == building && _view.IsOpen)
                        RefreshView();
                })
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _interaction.OnBuildingClicked -= OnBuildingClicked;
            _view.OnStartRecipeClicked -= OnStartRecipe;
            _view.OnStopSlotClicked -= OnStopSlot;
            _view.OnUpgradeClicked -= OnUpgrade;
            _view.OnCloseClicked -= OnClose;
            _view.OnAutoRepeatToggled -= OnAutoRepeatToggled;
            _disposables.Dispose();
        }

        public void Tick()
        {
            // Update progress bars every frame while panel is open
            if (!_view.IsOpen || _currentBuilding == null) return;

            // Check if the building still exists (may have been bulldozed)
            if (_currentBuilding.Instance == null)
            {
                _view.Hide();
                _currentBuilding = null;
                return;
            }

            var slots = _production.GetSlots(_currentBuilding);
            if (slots != null && slots.Count > 0)
                _view.SetActiveSlots(slots);
        }

        private void OnBuildingClicked(GameObject go)
        {
            if (go == null) return;

            var marker = go.GetComponentInParent<BuildingMarker>();
            if (marker == null || marker.PlacedObject == null) return;

            var placed = marker.PlacedObject;
            if (placed.Data == null || !placed.Data.Interactable) return;
            // Market buildings are handled by MarketPresenter, not this production panel.
            if (placed.Data.Role == BuildingRole.Market) return;


            // Toggle: clicking the same building closes the panel
            if (_view.IsOpen && _currentBuilding == placed)
            {
                _view.Hide();
                _currentBuilding = null;
                return;
            }

            _currentBuilding = placed;
            RefreshView();
            _view.Show();
        }

        private void RefreshView()
        {
            if (_currentBuilding == null || _currentBuilding.Data == null) return;

            var data = _currentBuilding.Data;

            _view.SetTitle(data.DisplayName, _currentBuilding.Level);
            _view.SetRecipes(data.Recipes, CanStartRecipe);
            _view.SetActiveSlots(_production.GetSlots(_currentBuilding));

            // Upgrade info
            int coinCost = UpgradeCoinCostPerLevel * _currentBuilding.Level;
            int plankCost = UpgradePlankCostPerLevel * _currentBuilding.Level;
            bool isMaxLevel = _currentBuilding.Level >= data.MaxLevel;
            bool canAfford = _wallet.CanAfford(coinCost) && _inventory.HasItem(PlankItemId, plankCost);
            _view.SetUpgradeInfo(coinCost, plankCost, canAfford, isMaxLevel);

            // Auto-repeat state: check first active slot, default true
            var slots = _production.GetSlots(_currentBuilding);
            bool autoRepeat = slots != null && slots.Count > 0 ? slots[0].AutoRepeat : true;
            _view.SetAutoRepeat(autoRepeat);
        }

        private bool CanStartRecipe(int recipeIndex)
        {
            if (_currentBuilding?.Data?.Recipes == null) return false;
            if (recipeIndex < 0 || recipeIndex >= _currentBuilding.Data.Recipes.Length) return false;

            // Check slot limit
            int maxSlots = _production.GetMaxSlots(_currentBuilding);
            var currentSlots = _production.GetSlots(_currentBuilding);
            if (currentSlots != null && currentSlots.Count >= maxSlots) return false;

            // Check ingredients
            var recipe = _currentBuilding.Data.Recipes[recipeIndex];
            foreach (var input in recipe.Inputs)
            {
                if (!_inventory.HasItem(input.ItemId, input.Amount)) return false;
            }
            return true;
        }

        private void OnStartRecipe(int recipeIndex)
        {
            if (_currentBuilding?.Data?.Recipes == null) return;
            if (recipeIndex < 0 || recipeIndex >= _currentBuilding.Data.Recipes.Length) return;

            var recipe = _currentBuilding.Data.Recipes[recipeIndex];
            _production.TryStartProduction(_currentBuilding, recipe);
            RefreshView();
        }

        private void OnStopSlot(int slotIndex)
        {
            if (_currentBuilding == null) return;
            _production.TryStopProduction(_currentBuilding, slotIndex);
            RefreshView();
        }

        private void OnUpgrade()
        {
            if (_currentBuilding?.Data == null) return;
            if (_currentBuilding.Level >= _currentBuilding.Data.MaxLevel) return;

            int coinCost = UpgradeCoinCostPerLevel * _currentBuilding.Level;
            int plankCost = UpgradePlankCostPerLevel * _currentBuilding.Level;

            if (!_wallet.TrySpend(coinCost)) return;
            if (!_inventory.TryConsume(PlankItemId, plankCost))
            {
                // Refund coins if planks insufficient
                _wallet.Add(coinCost);
                return;
            }

            _currentBuilding.Level++;
            RefreshView();
        }

        private void OnClose()
        {
            _view.Hide();
            _currentBuilding = null;
        }

        private void OnAutoRepeatToggled(bool enabled)
        {
            if (_currentBuilding == null) return;
            _production.SetAutoRepeat(_currentBuilding, enabled);
        }
    }
}
