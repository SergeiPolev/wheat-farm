using WheatFarm.Core.Data;
using WheatFarm.Inventory;

namespace WheatFarm.Economy
{
    public interface IShopService
    {
        bool TryBuySeed(PlantData plant, int amount = 1);
        bool TryBuyDye(DyeData dye, int amount = 1);
        bool TryBuyFertilizer(int amount = 1);
    }

    public class ShopService : IShopService
    {
        private readonly IWalletService _wallet;
        private readonly IInventoryService _inventory;

        private const int FertilizerCost = 10;
        private const string FertilizerId = "fertilizer";

        public ShopService(IWalletService wallet, IInventoryService inventory)
        {
            _wallet = wallet;
            _inventory = inventory;
        }

        public bool TryBuySeed(PlantData plant, int amount = 1)
        {
            if (plant == null) return false;
            int totalCost = plant.SeedCost * amount;
            if (!_wallet.TrySpend(totalCost)) return false;

            var item = new InventoryItem($"seed_{plant.PlantId}", ItemType.Seed, amount);
            if (!_inventory.TryAdd(item))
            {
                // Refund if inventory full
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
    }
}
