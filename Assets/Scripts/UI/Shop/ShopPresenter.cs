using System;
using System.Collections.Generic;
using R3;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Economy;

namespace WheatFarm.UI
{
    /// <summary>
    /// Shop presenter — populates catalog from PlantDatabase, handles buy actions.
    /// </summary>
    public class ShopPresenter : IInitializable, IDisposable
    {
        private readonly ShopView _view;
        private readonly IShopService _shop;
        private readonly IWalletService _wallet;
        private readonly PlantDatabase _plantDb;
        private readonly CompositeDisposable _disposables = new();

        private readonly List<PlantData> _catalog = new();

        public ShopPresenter(
            ShopView view,
            IShopService shop,
            IWalletService wallet,
            PlantDatabase plantDb)
        {
            _view = view;
            _shop = shop;
            _wallet = wallet;
            _plantDb = plantDb;
        }

        public void Initialize()
        {
            BuildCatalog();

            _view.OnBuyClicked += OnBuy;

            _wallet.Coins
                .Subscribe(c =>
                {
                    _view.UpdateCoins(c);
                    RefreshAffordability(c);
                })
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _view.OnBuyClicked -= OnBuy;
            _disposables.Dispose();
        }

        private void BuildCatalog()
        {
            _catalog.Clear();
            var names = new List<string>();
            var prices = new List<int>();

            // Seeds from plant database
            if (_plantDb != null)
            {
                foreach (var plant in _plantDb.Plants)
                {
                    _catalog.Add(plant);
                    names.Add($"{plant.DisplayName} seed");
                    prices.Add(plant.SeedCost);
                }
            }

            _view.SetCatalog(names.ToArray(), prices.ToArray());
            RefreshAffordability(_wallet.Coins.CurrentValue);
        }

        private void RefreshAffordability(int coins)
        {
            for (int i = 0; i < _catalog.Count; i++)
            {
                _view.SetItemInteractable(i, coins >= _catalog[i].SeedCost);
            }
        }

        private void OnBuy(int index)
        {
            if (index < 0 || index >= _catalog.Count) return;
            _shop.TryBuySeed(_catalog[index]);
        }
    }
}
