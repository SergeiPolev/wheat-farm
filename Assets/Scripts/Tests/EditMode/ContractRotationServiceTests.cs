using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Inventory;

namespace WheatFarm.Tests
{
    public class ContractRotationServiceTests
    {
        private WalletService _wallet;
        private InventoryService _inventory;
        private PlantUnlockService _plants;
        private DyeUnlockService _dyes;
        private BuildingUnlockService _buildings;
        private ContractService _contracts;
        private PlantDatabase _plantDb;
        private PlaceableDatabase _placeableDb;

        [SetUp]
        public void SetUp()
        {
            _wallet = new WalletService();
            _inventory = new InventoryService();
            _plantDb = PlantDb(
                Plant("wheat", unlockedByDefault: true),
                Plant("rose", unlockedByDefault: false));
            _plants = new PlantUnlockService(_plantDb);
            _dyes = new DyeUnlockService(_wallet);
            _buildings = new BuildingUnlockService(_wallet);
            _contracts = new ContractService(_wallet, _inventory, _plants, _dyes, _buildings);
            _placeableDb = PlaceableDb(
                Producer("mill", byDefault: true, "flour"),
                Producer("bakery", byDefault: false, "bread"));
        }

        [TearDown]
        public void TearDown()
        {
            _contracts.Dispose();
            _inventory.Dispose();
            _wallet.Dispose();
        }

        private static PlantData Plant(string id, bool unlockedByDefault)
        {
            var p = ScriptableObject.CreateInstance<PlantData>();
            p.PlantId = id;
            p.UnlockedByDefault = unlockedByDefault;
            return p;
        }

        private static PlantDatabase PlantDb(params PlantData[] plants)
        {
            var db = ScriptableObject.CreateInstance<PlantDatabase>();
            db.Plants = plants;
            return db;
        }

        private static ContractData Contract(
            string id, string plantReward = null, string dyeReward = null, params ItemStack[] required)
        {
            return new ContractData
            {
                ContractId = id,
                Description = id,
                Required = required ?? new ItemStack[0],
                CoinReward = 10,
                UnlockPlantId = plantReward,
                UnlockDyeId = dyeReward
            };
        }

        private static ContractDatabase Db(params ContractData[] contracts)
        {
            var db = ScriptableObject.CreateInstance<ContractDatabase>();
            db.Contracts = contracts;
            return db;
        }

        private static PlaceableData Producer(string buildingId, bool byDefault, params string[] outputs)
        {
            var p = ScriptableObject.CreateInstance<PlaceableData>();
            p.PlaceableId = buildingId;
            p.UnlockedByDefault = byDefault;
            p.Category = PlaceableCategory.Building;
            p.Recipes = System.Array.ConvertAll(outputs, o =>
            {
                var r = ScriptableObject.CreateInstance<RecipeData>();
                r.RecipeId = buildingId + "_" + o;
                r.Inputs = new[] { new ItemStack("wheat", 1) };
                r.Output = new ItemStack(o, 1);
                return r;
            });
            return p;
        }

        private static PlaceableDatabase PlaceableDb(params PlaceableData[] placeables)
        {
            var db = ScriptableObject.CreateInstance<PlaceableDatabase>();
            db.Items = placeables;
            return db;
        }

        private ContractRotationService Service(ContractDatabase db) =>
            new(db, null, _plants, _dyes, _contracts, _plantDb, _placeableDb, _buildings);

        [Test]
        public void Eligible_ExcludesAlreadyUnlockedReward()
        {
            var db = Db(Contract("c1", plantReward: "rose", required: new ItemStack("wheat", 5)));
            var svc = Service(db);

            Assert.AreEqual(1, svc.SelectEligible(10, 0).Count);

            _plants.Unlock("rose");
            Assert.AreEqual(0, svc.SelectEligible(10, 0).Count);
        }

        [Test]
        public void Eligible_ExcludesRequirementsFromLockedPlants()
        {
            var db = Db(
                Contract("locked-req", required: new ItemStack("rose", 3)),   // rose plant locked
                Contract("open-req", required: new ItemStack("wheat", 3)),    // wheat unlocked
                Contract("produced", required: new ItemStack("flour", 2)));   // not a plant → obtainable
            var svc = Service(db);

            var ids = svc.SelectEligible(10, 0).Select(c => c.ContractId).ToArray();
            CollectionAssert.DoesNotContain(ids, "locked-req");
            CollectionAssert.Contains(ids, "open-req");
            CollectionAssert.Contains(ids, "produced");
        }

        [Test]
        public void Eligible_ExcludesActive()
        {
            var c1 = Contract("c1", required: new ItemStack("wheat", 5));
            var db = Db(c1, Contract("c2", required: new ItemStack("wheat", 3)));
            var svc = Service(db);

            _contracts.AcceptContract(c1);

            var ids = svc.SelectEligible(10, 0).Select(c => c.ContractId).ToArray();
            CollectionAssert.DoesNotContain(ids, "c1");
            CollectionAssert.Contains(ids, "c2");
        }

        [Test]
        public void Rotate_RespectsCount()
        {
            var db = Db(
                Contract("c1", required: new ItemStack("wheat", 1)),
                Contract("c2", required: new ItemStack("wheat", 2)),
                Contract("c3", required: new ItemStack("wheat", 3)),
                Contract("c4", required: new ItemStack("wheat", 4)),
                Contract("c5", required: new ItemStack("wheat", 5)));
            var svc = Service(db);

            Assert.AreEqual(2, svc.SelectEligible(2, 0).Count);
            Assert.LessOrEqual(svc.SelectEligible(10, 0).Count, 5);
        }

        [Test]
        public void SaveLoad_RoundTrips()
        {
            var db = Db(
                Contract("c1", required: new ItemStack("wheat", 1)),
                Contract("c2", required: new ItemStack("wheat", 2)),
                Contract("c3", required: new ItemStack("wheat", 3)));
            var svc = Service(db);

            svc.LoadFrom(new[] { "c1", "c3" }, 7);
            Assert.AreEqual(7, svc.DayIndex);
            CollectionAssert.AreEquivalent(new[] { "c1", "c3" },
                svc.Available.Select(c => c.ContractId).ToArray());

            var (ids, day) = svc.ToSave();
            Assert.AreEqual(7, day);
            CollectionAssert.AreEquivalent(new[] { "c1", "c3" }, ids);
        }

        [Test]
        public void Eligible_ExcludesItemsProducedOnlyByLockedBuildings()
        {
            var db = Db(
                Contract("bread-req", required: new ItemStack("bread", 3)),   // bakery locked
                Contract("flour-req", required: new ItemStack("flour", 2)),   // mill unlocked
                Contract("wood-req", required: new ItemStack("wood", 2)));    // not a recipe output
            var svc = Service(db);

            var ids = svc.SelectEligible(10, 0).Select(c => c.ContractId).ToArray();
            CollectionAssert.DoesNotContain(ids, "bread-req");
            CollectionAssert.Contains(ids, "flour-req");
            CollectionAssert.Contains(ids, "wood-req");
        }

        [Test]
        public void Eligible_IncludesProducedItem_AfterBuildingUnlocked()
        {
            var db = Db(Contract("bread-req", required: new ItemStack("bread", 3)));
            var svc = Service(db);

            Assert.AreEqual(0, svc.SelectEligible(10, 0).Count);
            _buildings.Grant("bakery");
            Assert.AreEqual(1, svc.SelectEligible(10, 0).Count);
        }

        [Test]
        public void Eligible_ExcludesAlreadyUnlockedBuildingReward()
        {
            var c = Contract("c1", required: new ItemStack("wheat", 5));
            c.UnlockBuildingId = "bakery";
            var db = Db(c);
            var svc = Service(db);

            Assert.AreEqual(1, svc.SelectEligible(10, 0).Count);
            _buildings.Grant("bakery");
            Assert.AreEqual(0, svc.SelectEligible(10, 0).Count);
        }

        [Test]
        public void Eligible_ExcludesDefaultUnlockedBuildingReward()
        {
            var c = Contract("c1", required: new ItemStack("wheat", 5));
            c.UnlockBuildingId = "mill"; // UnlockedByDefault — never in the granted set
            var db = Db(c);
            var svc = Service(db);

            Assert.AreEqual(0, svc.SelectEligible(10, 0).Count);
        }
    }
}
