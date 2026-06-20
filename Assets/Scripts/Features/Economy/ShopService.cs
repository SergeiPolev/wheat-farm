using System.Collections.Generic;
using WheatFarm.Core.Data;
using WheatFarm.Inventory;

namespace WheatFarm.Economy
{
        /// <summary>A harvest item the player can sell, with its unit price.</summary>
    public readonly struct SellableItem
    {
        public readonly string ItemId;
        public readonly string DisplayName;
        public readonly int Amount;
        public readonly int UnitPrice;

        public SellableItem(string itemId, string displayName, int amount, int unitPrice)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Amount = amount;
            UnitPrice = unitPrice;
        }

        public int Total => Amount * UnitPrice;
    }

public interface IShopService
    {
        bool TryBuySeed(PlantData plant, int amount = 1);
        bool TryBuyDye(DyeData dye, int amount = 1);
        bool TryBuyFertilizer(int amount = 1);

        /// <summary>
        /// Sell a harvest item from inventory for coins.
        /// Uses PlantDatabase to look up the sell price.
        /// </summary>
        bool TrySell(string itemId, int amount = 1);

        /// <summary>Unlock a plant for coins (no-op if already unlocked). Persisted via save.</summary>
        bool TryUnlockPlant(PlantData plant);

        /// <summary>Current inventory harvest items with their unit sell prices.</summary>
        IReadOnlyList<SellableItem> GetSellableHarvest();

        /// <summary>Sell every harvest item for coins. Returns total coins earned.</summary>
        int SellAllHarvest();


    }

    public class ShopService : IShopService
    {
        private readonly IWalletService _wallet;
        private readonly IInventoryService _inventory;
        private readonly PlantDatabase _plantDb;        private readonly IPlantUnlockService _unlock;


        private const int FertilizerCost = 10;
        private const string FertilizerId = "fertilizer";

        public ShopService(IWalletService wallet, IInventoryService inventory, PlantDatabase plantDb, IPlantUnlockService unlock)
        {
            _wallet = wallet;
            _inventory = inventory;
            _plantDb = plantDb;            _unlock = unlock;

        }

        public bool TryBuySeed(PlantData plant, int amount = 1)
        {
            if (plant == null) return false;
            int totalCost = plant.SeedCost * amount;
            if (!_wallet.TrySpend(totalCost)) return false;

            var item = new InventoryItem($"seed_{plant.PlantId}", ItemType.Seed, amount);
            if (!_inventory.TryAdd(item))
            {
                _wallet.Add(totalCost);
                return false;
            }
            return true;
        }

        public bool TryBuyDye(DyeData dye, int amount = 1)
        {
            if (dye == null) return false;
            int totalCost = dye.Cost * amount;
            if (!_wallet.TrySpend(totalCost)) return false;

            var item = new InventoryItem($"dye_{dye.DyeId}", ItemType.Dye, amount);
            if (!_inventory.TryAdd(item))
            {
                _wallet.Add(totalCost);
                return false;
            }
            return true;
        }

        public bool TryBuyFertilizer(int amount = 1)
        {
            int totalCost = FertilizerCost * amount;
            if (!_wallet.TrySpend(totalCost)) return false;

            var item = new InventoryItem(FertilizerId, ItemType.Fertilizer, amount);
            if (!_inventory.TryAdd(item))
            {
                _wallet.Add(totalCost);
                return false;
            }
            return true;
        }

        public bool TrySell(string itemId, int amount = 1)
        {
            if (!_inventory.HasItem(itemId, amount)) return false;

            // Look up sell price from PlantDatabase
            var plantData = _plantDb.GetById(itemId);
            if (plantData == null) return false;

            int totalPrice = plantData.SellPrice * amount;

            if (!_inventory.TryConsume(itemId, amount)) return false;

            _wallet.Add(totalPrice);
            return true;
        }

        public bool TryUnlockPlant(PlantData plant)
        {
            if (plant == null) return false;
            if (_unlock.IsUnlocked(plant.PlantId)) return true;

            int price = plant.SeedCost * 10;
            if (price > 0 && !_wallet.TrySpend(price)) return false;

            _unlock.Unlock(plant.PlantId);
            return true;
        }

        public IReadOnlyList<SellableItem> GetSellableHarvest()
        {
            var result = new List<SellableItem>();
            foreach (var item in _inventory.Items)
            {
                if (item.Type != ItemType.Harvest) continue;
                var pd = _plantDb.GetById(item.ItemId);
                int unit = pd != null ? pd.SellPrice : 0;
                string name = pd != null ? pd.DisplayName : item.ItemId;
                result.Add(new SellableItem(item.ItemId, name, item.Amount, unit));
            }
            return result;
        }

        public int SellAllHarvest()
        {
            int earned = 0;
            // Snapshot first — selling mutates the inventory collection.
            var snapshot = GetSellableHarvest();
            foreach (var s in snapshot)
            {
                if (s.UnitPrice <= 0 || s.Amount <= 0) continue;
                if (_inventory.TryConsume(s.ItemId, s.Amount))
                {
                    int gain = s.UnitPrice * s.Amount;
                    _wallet.Add(gain);
                    earned += gain;
                }
            }
            return earned;
        }


    }
}
