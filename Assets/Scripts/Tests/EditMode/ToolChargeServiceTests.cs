using System.Collections.Generic;
using NUnit.Framework;
using R3;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Farming;
using WheatFarm.Inventory;
using WheatFarm.Player.Charges;
using WheatFarm.Player.Tools;

namespace WheatFarm.Tests
{
    public class ToolChargeServiceTests
    {
        private InventoryService _inventory;
        private ToolChargeService _charges;
        private ToolChargeConfig _config;

        [SetUp]
        public void SetUp()
        {
            _inventory = new InventoryService();
            _config = new ToolChargeConfig { WaterMax = 10, DyeMax = 10, SeedMax = 5, BasketMax = 3 };
            _charges = new ToolChargeService(_inventory, _config);
        }

        [TearDown]
        public void TearDown()
        {
            _charges.Dispose();
            _inventory.Dispose();
        }

        [Test]
        public void NewGame_StartsWithFullWaterAndDye_EmptyBagAndBasket()
        {
            Assert.AreEqual(10, _charges.Water.CurrentValue);
            Assert.AreEqual(10, _charges.Dye.CurrentValue);
            Assert.AreEqual(0, _charges.Seeds.CurrentValue);
            Assert.AreEqual(0, _charges.Basket.CurrentValue);
            Assert.IsTrue(_charges.BasketHasRoom);
        }

        [Test]
        public void Water_DrainsToZero_ThenRefusesAndRefillCapsAtMax()
        {
            for (int i = 0; i < 10; i++) Assert.IsTrue(_charges.TryUseWater());
            Assert.IsFalse(_charges.TryUseWater());
            Assert.IsFalse(_charges.HasWater);

            Assert.AreEqual(4, _charges.RefillWater(4));
            Assert.AreEqual(6, _charges.RefillWater(100));
            Assert.AreEqual(0, _charges.RefillWater(5));
            Assert.AreEqual(10, _charges.Water.CurrentValue);
        }

        [Test]
        public void LoadSeeds_PullsFromInventory_CappedByBagAndStock()
        {
            _inventory.TryAdd(new InventoryItem("seed_wheat", ItemType.Seed, 3));
            Assert.AreEqual(3, _charges.LoadSeeds("wheat", 100));
            Assert.AreEqual(0, _inventory.GetAmount("seed_wheat"));
            Assert.AreEqual("wheat", _charges.SeedPlantId.CurrentValue);

            _inventory.TryAdd(new InventoryItem("seed_wheat", ItemType.Seed, 10));
            Assert.AreEqual(2, _charges.LoadSeeds("wheat", 100)); // bag max 5
            Assert.AreEqual(8, _inventory.GetAmount("seed_wheat"));
        }

        [Test]
        public void LoadSeeds_OtherType_ReturnsBaggedSeedsFirst()
        {
            _inventory.TryAdd(new InventoryItem("seed_wheat", ItemType.Seed, 4));
            _inventory.TryAdd(new InventoryItem("seed_corn", ItemType.Seed, 2));
            _charges.LoadSeeds("wheat", 4);

            Assert.AreEqual(2, _charges.LoadSeeds("corn", 10));
            Assert.AreEqual(4, _inventory.GetAmount("seed_wheat"), "wheat went back to inventory");
            Assert.AreEqual("corn", _charges.SeedPlantId.CurrentValue);
            Assert.IsFalse(_charges.CanSow("wheat"));
            Assert.IsTrue(_charges.CanSow("corn"));
        }

        [Test]
        public void TrySow_EmptiesBag_AndClearsType()
        {
            _inventory.TryAdd(new InventoryItem("seed_wheat", ItemType.Seed, 2));
            _charges.LoadSeeds("wheat", 2);
            Assert.IsTrue(_charges.TrySow("wheat"));
            Assert.IsTrue(_charges.TrySow("wheat"));
            Assert.IsFalse(_charges.TrySow("wheat"));
            Assert.IsNull(_charges.SeedPlantId.CurrentValue);
        }

        [Test]
        public void Basket_FillsToMax_UnloadMovesCropsThenBonusSeeds()
        {
            Assert.IsTrue(_charges.TryStoreHarvest("wheat", "seed_wheat", 2));
            Assert.IsTrue(_charges.TryStoreHarvest("wheat"));
            Assert.IsTrue(_charges.TryStoreHarvest("corn"));
            Assert.IsFalse(_charges.TryStoreHarvest("corn"), "basket full");
            Assert.IsFalse(_charges.BasketHasRoom);

            Assert.AreEqual(2, _charges.UnloadBasket(2));
            Assert.AreEqual(2, _inventory.GetAmount("wheat"));
            Assert.AreEqual(0, _inventory.GetAmount("seed_wheat"), "bonus waits until basket is empty");

            Assert.AreEqual(1, _charges.UnloadBasket(5));
            Assert.AreEqual(1, _inventory.GetAmount("corn"));
            Assert.AreEqual(2, _inventory.GetAmount("seed_wheat"));
            Assert.AreEqual(0, _charges.BasketContents.Count);
        }

        [Test]
        public void Snapshot_RoundTrips()
        {
            _inventory.TryAdd(new InventoryItem("seed_corn", ItemType.Seed, 3));
            _charges.LoadSeeds("corn", 3);
            _charges.TryUseWater();
            _charges.TryUseDye(); _charges.TryUseDye();
            _charges.TryStoreHarvest("tomato", "seed_tomato", 1);

            var json = JsonUtility.ToJson(_charges.Snapshot());
            var other = new ToolChargeService(new InventoryService(), _config);
            other.Restore(JsonUtility.FromJson<ToolChargeSnapshot>(json));

            Assert.AreEqual(9, other.Water.CurrentValue);
            Assert.AreEqual(8, other.Dye.CurrentValue);
            Assert.AreEqual(3, other.Seeds.CurrentValue);
            Assert.AreEqual("corn", other.SeedPlantId.CurrentValue);
            Assert.AreEqual(1, other.Basket.CurrentValue);
            Assert.AreEqual(1, other.BasketContents["seed_tomato"]);
            other.Dispose();
        }

        [Test]
        public void DepletedEvent_Fires()
        {
            var got = new List<ChargeKind>();
            using var sub = _charges.OnDepleted.Subscribe(got.Add);
            _charges.ReportDepleted(ChargeKind.Basket);
            CollectionAssert.AreEqual(new[] { ChargeKind.Basket }, got);
        }
    }

    /// <summary>Minimal plant system: Water marks the cell watered.</summary>
    internal class FakePlantSystem : IPlantSystem
    {
        private readonly IChunkSystem _chunks;
        public int WaterCalls;
        public FakePlantSystem(IChunkSystem chunks) { _chunks = chunks; }
        public Subject<HarvestData> OnHarvested { get; } = new();
        public bool Plant(Vector2Int c, int x, int y, PlantData d) => false;
        public void Water(Vector2Int c, int x, int y)
        {
            WaterCalls++;
            var ch = _chunks.GetChunk(c);
            ch.Cells[ch.CellIndex(x, y)].Watered = true;
        }
        public void Fertilize(Vector2Int c, int x, int y, float m) { }
        public HarvestData? Harvest(Vector2Int c, int x, int y) => null;
        public void Uproot(Vector2Int c, int x, int y) { }
        public void Dye(Vector2Int c, int x, int y, Color color) { }
        public void ForceMature(Vector2Int c, int x, int y) { }
    }

    public class WateringCanChargeTests
    {
        [Test]
        public void Watering_SpendsOnePerThirstyCell_AndNothingOnSecondPass()
        {
            var chunks = new ChunkSystem(chunkWorldSize: 4f, subCellResolution: 8);
            chunks.TryUnlockChunk(Vector2Int.zero);
            var chunk = chunks.GetChunk(Vector2Int.zero);
            for (int i = 0; i < chunk.CellCount; i++) chunk.Cells[i].PlantId = "wheat";

            var brush = new BrushService(chunks);
            brush.CurrentSize.Value = BrushSize.Medium;
            var inv = new InventoryService();
            var charges = new ToolChargeService(inv, new ToolChargeConfig { WaterMax = 500 });
            var plants = new FakePlantSystem(chunks);
            var tool = new WateringCanTool(plants, brush, charges);

            var pos = chunks.CellToWorld(Vector2Int.zero, 4, 4);
            tool.UseAtPosition(pos);
            int spent = 500 - charges.Water.CurrentValue;
            Assert.Greater(spent, 0);
            Assert.AreEqual(plants.WaterCalls, spent);

            tool.UseAtPosition(pos);
            Assert.AreEqual(500 - spent, charges.Water.CurrentValue, "already-watered cells cost nothing");

            charges.Dispose(); inv.Dispose(); brush.Dispose(); chunks.Dispose();
        }

        [Test]
        public void Watering_WithEmptyCan_ReportsDepleted_AndWatersNothing()
        {
            var chunks = new ChunkSystem(chunkWorldSize: 4f, subCellResolution: 8);
            chunks.TryUnlockChunk(Vector2Int.zero);
            var chunk = chunks.GetChunk(Vector2Int.zero);
            for (int i = 0; i < chunk.CellCount; i++) chunk.Cells[i].PlantId = "wheat";
            var brush = new BrushService(chunks);
            var inv = new InventoryService();
            var charges = new ToolChargeService(inv, new ToolChargeConfig { WaterMax = 1 });
            charges.TryUseWater();
            var plants = new FakePlantSystem(chunks);
            var tool = new WateringCanTool(plants, brush, charges);
            int depleted = 0;
            using var sub = charges.OnDepleted.Subscribe(_ => depleted++);

            tool.UseAtPosition(chunks.CellToWorld(Vector2Int.zero, 4, 4));
            Assert.AreEqual(0, plants.WaterCalls);
            Assert.AreEqual(1, depleted);

            charges.Dispose(); inv.Dispose(); brush.Dispose(); chunks.Dispose();
        }
    }
}
