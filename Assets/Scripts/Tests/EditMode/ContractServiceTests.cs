using NUnit.Framework;
using R3;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Inventory;

namespace WheatFarm.Tests
{
    public class ContractServiceTests
    {
        private WalletService _wallet;
        private InventoryService _inventory;
        private PlantUnlockService _plants;
        private DyeUnlockService _dyes;
        private BuildingUnlockService _buildings;
        private ContractService _svc;

        [SetUp]
        public void SetUp()
        {
            _wallet = new WalletService();
            _wallet.SetCoins(0);
            _inventory = new InventoryService();
            _plants = new PlantUnlockService(ScriptableObject.CreateInstance<PlantDatabase>());
            _dyes = new DyeUnlockService(_wallet);
            _buildings = new BuildingUnlockService(_wallet);
            _svc = new ContractService(_wallet, _inventory, _plants, _dyes, _buildings);
        }

        [TearDown]
        public void TearDown()
        {
            _svc.Dispose();
            _inventory.Dispose();
            _wallet.Dispose();
        }

        private static ContractData Contract(
            int coins = 50, string plantId = null, string dyeId = null, params ItemStack[] required)
        {
            return new ContractData
            {
                ContractId = "test",
                Description = "test contract",
                Required = required,
                CoinReward = coins,
                UnlockPlantId = plantId,
                UnlockDyeId = dyeId
            };
        }

        [Test]
        public void Complete_WithBuildingReward_GrantsBuilding()
        {
            _inventory.TryAdd(new InventoryItem("flour", ItemType.Product, 3));
            var c = Contract(150, null, null, new ItemStack("flour", 3));
            c.UnlockBuildingId = "bakery";
            _svc.AcceptContract(c);

            Assert.IsTrue(_svc.TryCompleteContract(0));
            CollectionAssert.Contains(_buildings.UnlockedIds, "bakery");
        }

        [Test]
        public void Complete_WithItems_ConsumesPaysAndUnlocks()
        {
            _inventory.TryAdd(new InventoryItem("wheat", ItemType.Harvest, 5));
            _svc.AcceptContract(Contract(50, "rose", "red", new ItemStack("wheat", 5)));
            bool fired = false;
            _svc.OnContractCompleted.Subscribe(_ => fired = true);

            Assert.IsTrue(_svc.TryCompleteContract(0));
            Assert.AreEqual(0, _inventory.GetAmount("wheat"));
            Assert.AreEqual(50, _wallet.Coins.CurrentValue);
            Assert.IsTrue(_plants.IsUnlocked("rose"));
            CollectionAssert.Contains(_dyes.UnlockedIds, "red");
            Assert.AreEqual(0, _svc.ActiveContracts.Count);
            Assert.IsTrue(fired);
        }

        [Test]
        public void Complete_WithoutItems_DoesNothing()
        {
            _inventory.TryAdd(new InventoryItem("wheat", ItemType.Harvest, 2));
            _svc.AcceptContract(Contract(50, null, null, new ItemStack("wheat", 5)));

            Assert.IsFalse(_svc.TryCompleteContract(0));
            Assert.AreEqual(2, _inventory.GetAmount("wheat"));
            Assert.AreEqual(0, _wallet.Coins.CurrentValue);
            Assert.AreEqual(1, _svc.ActiveContracts.Count);
        }

        [Test]
        public void Complete_MultiRequirement_AllOrNothing()
        {
            _inventory.TryAdd(new InventoryItem("wheat", ItemType.Harvest, 3));
            _svc.AcceptContract(Contract(50, null, null,
                new ItemStack("wheat", 3), new ItemStack("flour", 2)));

            Assert.IsFalse(_svc.TryCompleteContract(0));
            Assert.AreEqual(3, _inventory.GetAmount("wheat")); // nothing consumed
            Assert.AreEqual(0, _wallet.Coins.CurrentValue);
            Assert.AreEqual(1, _svc.ActiveContracts.Count);
        }

        [Test]
        public void Abandon_RemovesWithoutReward()
        {
            _svc.AcceptContract(Contract(50, null, null, new ItemStack("wheat", 5)));

            _svc.AbandonContract(0);

            Assert.AreEqual(0, _svc.ActiveContracts.Count);
            Assert.AreEqual(0, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void CanComplete_ReflectsInventoryCoverage()
        {
            _svc.AcceptContract(Contract(50, null, null,
                new ItemStack("wheat", 3), new ItemStack("flour", 2)));

            Assert.IsFalse(_svc.CanComplete(_svc.ActiveContracts[0]));

            _inventory.TryAdd(new InventoryItem("wheat", ItemType.Harvest, 3));
            Assert.IsFalse(_svc.CanComplete(_svc.ActiveContracts[0]));

            _inventory.TryAdd(new InventoryItem("flour", ItemType.Product, 2));
            Assert.IsTrue(_svc.CanComplete(_svc.ActiveContracts[0]));
        }
    }
}
