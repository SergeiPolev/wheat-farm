using System;
using System.Collections.Generic;
using R3;
using WheatFarm.Core.Data;
using WheatFarm.Inventory;

namespace WheatFarm.Player.Charges
{
    /// <summary>What a tool carries: water in the can, paint in the bucket, seeds in the bag, crops in the basket.</summary>
    public enum ChargeKind { Water, Dye, Seeds, Basket }

    /// <summary>Capacities and refill pacing for the tool-charge loop.</summary>
    [Serializable]
    public class ToolChargeConfig
    {
        public int WaterMax = 80;
        public int DyeMax = 80;
        public int SeedMax = 40;
        public int BasketMax = 60;

        /// <summary>Seconds a station needs to take a container from empty to full.</summary>
        public float FullRefillSeconds = 1.4f;

        /// <summary>How close (world units, to the station's bounds) the player must stand.</summary>
        public float StationRadius = 1.4f;

        public const string WellId = "well";
        public const string WarehouseId = "warehouse";
    }

    /// <summary>Serializable snapshot for save/load.</summary>
    [Serializable]
    public class ToolChargeSnapshot
    {
        public int Water;
        public int Dye;
        public string SeedPlantId;
        public int Seeds;
        public List<string> BasketIds = new();
        public List<int> BasketAmounts = new();
        public int BasketCrops;
    }

    public interface IToolChargeService
    {
        ToolChargeConfig Config { get; }
        ReadOnlyReactiveProperty<int> Water { get; }
        ReadOnlyReactiveProperty<int> Dye { get; }
        ReadOnlyReactiveProperty<int> Seeds { get; }
        /// <summary>Crops currently in the basket (fill level, not room left).</summary>
        ReadOnlyReactiveProperty<int> Basket { get; }
        /// <summary>Plant whose seeds are in the bag (null when the bag is empty).</summary>
        ReadOnlyReactiveProperty<string> SeedPlantId { get; }
        IReadOnlyDictionary<string, int> BasketContents { get; }

        /// <summary>Fired when a tool was used but its container could not serve the action.</summary>
        Observable<ChargeKind> OnDepleted { get; }
        /// <summary>Fired per refill/unload step with the amount moved.</summary>
        Observable<(ChargeKind kind, int amount)> OnTransferred { get; }

        int Max(ChargeKind kind);
        int Current(ChargeKind kind);
        /// <summary>Fill fraction 0..1. Basket: how full the basket is.</summary>
        float Fraction(ChargeKind kind);

        bool HasWater { get; }
        bool HasDye { get; }
        bool BasketHasRoom { get; }
        bool CanSow(string plantId);

        bool TryUseWater();
        bool TryUseDye();
        bool TrySow(string plantId);
        /// <summary>Put one harvested crop (plus bonus items) in the basket.</summary>
        bool TryStoreHarvest(string cropId, string bonusId = null, int bonusAmount = 0);

        void ReportDepleted(ChargeKind kind);

        int RefillWater(int maxAmount);
        int RefillDye(int maxAmount);
        /// <summary>Load seeds of plantId from inventory (returns a different bagged type first).</summary>
        int LoadSeeds(string plantId, int maxAmount);
        /// <summary>Move up to maxCrops crops (and all bonus items) from basket to inventory.</summary>
        int UnloadBasket(int maxCrops);
        /// <summary>Return bagged seeds to inventory (e.g. before switching type).</summary>
        void ReturnSeeds();

        ToolChargeSnapshot Snapshot();
        void Restore(ToolChargeSnapshot snapshot);
        void ResetToNewGame();
    }

    public class ToolChargeService : IToolChargeService, IDisposable
    {
        private readonly IInventoryService _inventory;
        private readonly ReactiveProperty<int> _water = new();
        private readonly ReactiveProperty<int> _dye = new();
        private readonly ReactiveProperty<int> _seeds = new();
        private readonly ReactiveProperty<int> _basket = new();
        private readonly ReactiveProperty<string> _seedPlantId = new();
        private readonly Dictionary<string, int> _basketContents = new();
        private readonly Dictionary<string, ItemType> _basketTypes = new();
        private readonly List<string> _basketOrder = new();
        private readonly Subject<ChargeKind> _depleted = new();
        private readonly Subject<(ChargeKind, int)> _transferred = new();

        public ToolChargeConfig Config { get; }
        public ReadOnlyReactiveProperty<int> Water => _water;
        public ReadOnlyReactiveProperty<int> Dye => _dye;
        public ReadOnlyReactiveProperty<int> Seeds => _seeds;
        public ReadOnlyReactiveProperty<int> Basket => _basket;
        public ReadOnlyReactiveProperty<string> SeedPlantId => _seedPlantId;
        public IReadOnlyDictionary<string, int> BasketContents => _basketContents;
        public Observable<ChargeKind> OnDepleted => _depleted;
        public Observable<(ChargeKind kind, int amount)> OnTransferred => _transferred;

        public ToolChargeService(IInventoryService inventory, ToolChargeConfig config = null)
        {
            _inventory = inventory;
            Config = config ?? new ToolChargeConfig();
            ResetToNewGame();
        }

        public void ResetToNewGame()
        {
            _water.Value = Config.WaterMax;
            _dye.Value = Config.DyeMax;
            _seeds.Value = 0;
            _seedPlantId.Value = null;
            _basket.Value = 0;
            _basketContents.Clear();
            _basketTypes.Clear();
            _basketOrder.Clear();
        }

        public int Max(ChargeKind kind) => kind switch
        {
            ChargeKind.Water => Config.WaterMax,
            ChargeKind.Dye => Config.DyeMax,
            ChargeKind.Seeds => Config.SeedMax,
            ChargeKind.Basket => Config.BasketMax,
            _ => 1
        };

        public int Current(ChargeKind kind) => kind switch
        {
            ChargeKind.Water => _water.Value,
            ChargeKind.Dye => _dye.Value,
            ChargeKind.Seeds => _seeds.Value,
            ChargeKind.Basket => _basket.Value,
            _ => 0
        };

        public float Fraction(ChargeKind kind)
        {
            int max = Max(kind);
            return max <= 0 ? 0f : Math.Clamp(Current(kind) / (float)max, 0f, 1f);
        }

        public bool HasWater => _water.Value > 0;
        public bool HasDye => _dye.Value > 0;
        public bool BasketHasRoom => _basket.Value < Config.BasketMax;
        public bool CanSow(string plantId) =>
            _seeds.Value > 0 && plantId != null && _seedPlantId.Value == plantId;

        public bool TryUseWater()
        {
            if (_water.Value <= 0) return false;
            _water.Value--;
            return true;
        }

        public bool TryUseDye()
        {
            if (_dye.Value <= 0) return false;
            _dye.Value--;
            return true;
        }

        public bool TrySow(string plantId)
        {
            if (!CanSow(plantId)) return false;
            _seeds.Value--;
            if (_seeds.Value == 0) _seedPlantId.Value = null;
            return true;
        }

        public bool TryStoreHarvest(string cropId, string bonusId = null, int bonusAmount = 0)
        {
            if (!BasketHasRoom || string.IsNullOrEmpty(cropId)) return false;
            AddToBasket(cropId, ItemType.Harvest, 1);
            if (!string.IsNullOrEmpty(bonusId) && bonusAmount > 0)
                AddToBasket(bonusId, ItemType.Seed, bonusAmount);
            _basket.Value++;
            return true;
        }

        private void AddToBasket(string id, ItemType type, int amount)
        {
            if (_basketContents.TryGetValue(id, out var have))
                _basketContents[id] = have + amount;
            else
            {
                _basketContents[id] = amount;
                _basketTypes[id] = type;
                _basketOrder.Add(id);
            }
        }

        public void ReportDepleted(ChargeKind kind) => _depleted.OnNext(kind);

        public int RefillWater(int maxAmount)
        {
            int n = Math.Min(maxAmount, Config.WaterMax - _water.Value);
            if (n <= 0) return 0;
            _water.Value += n;
            _transferred.OnNext((ChargeKind.Water, n));
            return n;
        }

        public int RefillDye(int maxAmount)
        {
            int n = Math.Min(maxAmount, Config.DyeMax - _dye.Value);
            if (n <= 0) return 0;
            _dye.Value += n;
            _transferred.OnNext((ChargeKind.Dye, n));
            return n;
        }

        public int LoadSeeds(string plantId, int maxAmount)
        {
            if (string.IsNullOrEmpty(plantId) || maxAmount <= 0) return 0;
            if (_seedPlantId.Value != null && _seedPlantId.Value != plantId)
                ReturnSeeds();

            string seedId = SeedItemId(plantId);
            int room = Config.SeedMax - _seeds.Value;
            int n = Math.Min(Math.Min(maxAmount, room), _inventory.GetAmount(seedId));
            if (n <= 0) return 0;
            if (!_inventory.TryConsume(seedId, n)) return 0;

            _seedPlantId.Value = plantId;
            _seeds.Value += n;
            _transferred.OnNext((ChargeKind.Seeds, n));
            return n;
        }

        public void ReturnSeeds()
        {
            if (_seeds.Value > 0 && _seedPlantId.Value != null)
                _inventory.TryAdd(new InventoryItem(SeedItemId(_seedPlantId.Value), ItemType.Seed, _seeds.Value));
            _seeds.Value = 0;
            _seedPlantId.Value = null;
        }

        public int UnloadBasket(int maxCrops)
        {
            if (_basket.Value <= 0 || maxCrops <= 0) return 0;
            int moved = 0;
            // Crops first (they count toward capacity), oldest type first.
            for (int i = 0; i < _basketOrder.Count && moved < maxCrops; i++)
            {
                var id = _basketOrder[i];
                if (_basketTypes[id] != ItemType.Harvest) continue;
                int take = Math.Min(_basketContents[id], maxCrops - moved);
                if (take <= 0) continue;
                if (!_inventory.TryAdd(new InventoryItem(id, ItemType.Harvest, take))) break; // inventory full
                _basketContents[id] -= take;
                moved += take;
            }

            if (moved > 0)
            {
                _basket.Value -= moved;
                _transferred.OnNext((ChargeKind.Basket, moved));
            }

            // Basket emptied of crops -> hand over bonus items (seeds) too.
            if (_basket.Value == 0)
            {
                foreach (var id in _basketOrder)
                {
                    if (_basketTypes[id] == ItemType.Harvest || _basketContents[id] <= 0) continue;
                    if (_inventory.TryAdd(new InventoryItem(id, _basketTypes[id], _basketContents[id])))
                        _basketContents[id] = 0;
                }
            }

            PruneBasket();
            return moved;
        }

        private void PruneBasket()
        {
            for (int i = _basketOrder.Count - 1; i >= 0; i--)
            {
                var id = _basketOrder[i];
                if (_basketContents[id] > 0) continue;
                _basketContents.Remove(id);
                _basketTypes.Remove(id);
                _basketOrder.RemoveAt(i);
            }
        }

        public static string SeedItemId(string plantId) => "seed_" + plantId;

        public ToolChargeSnapshot Snapshot()
        {
            var s = new ToolChargeSnapshot
            {
                Water = _water.Value,
                Dye = _dye.Value,
                SeedPlantId = _seedPlantId.Value,
                Seeds = _seeds.Value,
                BasketCrops = _basket.Value
            };
            foreach (var id in _basketOrder)
            {
                s.BasketIds.Add((_basketTypes[id] == ItemType.Harvest ? "h:" : "s:") + id);
                s.BasketAmounts.Add(_basketContents[id]);
            }
            return s;
        }

        public void Restore(ToolChargeSnapshot s)
        {
            if (s == null) { ResetToNewGame(); return; }
            _water.Value = Math.Clamp(s.Water, 0, Config.WaterMax);
            _dye.Value = Math.Clamp(s.Dye, 0, Config.DyeMax);
            _seeds.Value = Math.Clamp(s.Seeds, 0, Config.SeedMax);
            _seedPlantId.Value = _seeds.Value > 0 ? s.SeedPlantId : null;
            _basketContents.Clear();
            _basketTypes.Clear();
            _basketOrder.Clear();
            int crops = 0;
            for (int i = 0; i < s.BasketIds.Count && i < s.BasketAmounts.Count; i++)
            {
                var raw = s.BasketIds[i];
                bool harvest = !raw.StartsWith("s:");
                var id = raw.Length > 2 && raw[1] == ':' ? raw.Substring(2) : raw;
                AddToBasket(id, harvest ? ItemType.Harvest : ItemType.Seed, s.BasketAmounts[i]);
                if (harvest) crops += s.BasketAmounts[i];
            }
            _basket.Value = crops;
        }

        public void Dispose()
        {
            _water.Dispose();
            _dye.Dispose();
            _seeds.Dispose();
            _basket.Dispose();
            _seedPlantId.Dispose();
            _depleted.Dispose();
            _transferred.Dispose();
        }
    }
}
