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

        private ContractRotationService Service(ContractDatabase db) =>
            new(db, null, _plants, _dyes, _contracts, _plantDb);

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
    }
}
