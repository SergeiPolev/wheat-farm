using System;
using ObservableCollections;
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
    /// Market presenter — opens the Market tablet when the player clicks a Market building.
    /// Handles selling harvest (Sell All) and buying seeds for unlocked plants.
    /// </summary>
    public class MarketPresenter : IInitializable, IDisposable
    {
        private readonly MarketView _view;
        private readonly IShopService _shop;
        private readonly IInventoryService _inventory;
        private readonly IWalletService _wallet;
        private readonly IPlantUnlockService _unlock;
        private readonly FarmInteractionController _interaction;
        private readonly CompositeDisposable _disposables = new();

        private PlacedObject _current;
        private PlantData[] _buyPlants = Array.Empty<PlantData>();

        public MarketPresenter(
            MarketView view,
            IShopService shop,
            IInventoryService inventory,
            IWalletService wallet,
            IPlantUnlockService unlock,
            FarmInteractionController interaction)
        {
            _view = view;
            _shop = shop;
            _inventory = inventory;
            _wallet = wallet;
            _unlock = unlock;
            _interaction = interaction;
        }

        public void Initialize()
        {
            _interaction.OnBuildingClicked += OnBuildingClicked;
            _view.OnSellAllClicked += OnSellAll;
            _view.OnBuyClicked += OnBuy;
            _view.OnCloseClicked += Close;
            _inventory.Items.CollectionChanged += OnInventoryChanged;
            _unlock.Changed += OnUnlocksChanged;

            _wallet.Coins
                .Subscribe(c =>
                {
                    _view.UpdateCoins(c);
                    if (_view.IsOpen) Refresh();
                })
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _interaction.OnBuildingClicked -= OnBuildingClicked;
            _view.OnSellAllClicked -= OnSellAll;
            _view.OnBuyClicked -= OnBuy;
            _view.OnCloseClicked -= Close;
            _inventory.Items.CollectionChanged -= OnInventoryChanged;
            _unlock.Changed -= OnUnlocksChanged;
            _disposables.Dispose();
        }

        private void OnBuildingClicked(GameObject go)
        {
            if (go == null) return;

            var marker = go.GetComponentInParent<BuildingMarker>();
            if (marker == null || marker.PlacedObject == null) return;

            var placed = marker.PlacedObject;
            if (placed.Data == null || placed.Data.Role != BuildingRole.Market) return;

            if (_view.IsOpen && _current == placed)
            {
                Close();
                return;
            }

            _current = placed;
            Refresh();
            _view.Show();
        }

        private void OnInventoryChanged(in NotifyCollectionChangedEventArgs<InventoryItem> e)
        {
            if (_view.IsOpen) Refresh();
        }

        private void OnUnlocksChanged()
        {
            if (_view.IsOpen) Refresh();
        }

        private void Refresh()
        {
            // --- Sell list (harvest) ---
            var items = _shop.GetSellableHarvest();
            int n = items.Count;
            var names = new string[n];
            var amounts = new int[n];
            var totals = new int[n];
            int total = 0;
            for (int i = 0; i < n; i++)
            {
                names[i] = items[i].DisplayName;
                amounts[i] = items[i].Amount;
                totals[i] = items[i].Total;
                total += items[i].Total;
            }
            _view.SetItems(names, amounts, totals);
            _view.UpdateTotal(total, total > 0);

            // --- Buy list (seeds for unlocked plants) ---
            _buyPlants = _unlock.GetUnlocked();
            int coins = _wallet.Coins.CurrentValue;
            var bNames = new string[_buyPlants.Length];
            var bPrices = new int[_buyPlants.Length];
            var bAfford = new bool[_buyPlants.Length];
            for (int i = 0; i < _buyPlants.Length; i++)
            {
                bNames[i] = _buyPlants[i].DisplayName + " seed";
                bPrices[i] = _buyPlants[i].SeedCost;
                bAfford[i] = coins >= _buyPlants[i].SeedCost;
            }
            _view.SetBuyItems(bNames, bPrices, bAfford);

            _view.UpdateCoins(coins);
        }

        private void OnSellAll()
        {
            int earned = _shop.SellAllHarvest();
            Debug.Log($"[Market] Sold all harvest for {earned}c");
            Refresh();
        }

        private void OnBuy(int index)
        {
            if (index < 0 || index >= _buyPlants.Length) return;
            var plant = _buyPlants[index];
            if (_shop.TryBuySeed(plant))
                Debug.Log($"[Market] Bought a {plant.DisplayName} seed");
            else
                Debug.Log($"[Market] Cannot buy {plant.DisplayName} seed (not enough coins?)");
            Refresh();
        }

        private void Close()
        {
            _view.Hide();
            _current = null;
        }
    }
}
