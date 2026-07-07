using NUnit.Framework;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Economy;

namespace WheatFarm.Tests
{
    public class BuildingUnlockServiceTests
    {
        private WalletService _wallet;

        [SetUp]
        public void SetUp() => _wallet = new WalletService();

        [TearDown]
        public void TearDown() => _wallet.Dispose();

        private static PlaceableData Placeable(string id, bool byDefault, int unlockCost)
        {
            var p = ScriptableObject.CreateInstance<PlaceableData>();
            p.PlaceableId = id;
            p.UnlockedByDefault = byDefault;
            p.UnlockCost = unlockCost;
            return p;
        }

        [Test]
        public void DefaultUnlocked_IsUnlocked_WithoutBuying()
        {
            var svc = new BuildingUnlockService(_wallet);
            Assert.IsTrue(svc.IsUnlocked(Placeable("mill", true, 0)));
        }

        [Test]
        public void TryUnlock_Affordable_SpendsOnceAndUnlocks_AndFiresChanged()
        {
            _wallet.SetCoins(500);
            var svc = new BuildingUnlockService(_wallet);
            var bakery = Placeable("bakery", false, 300);
            bool changed = false;
            svc.Changed += () => changed = true;

            Assert.IsTrue(svc.TryUnlock(bakery));
            Assert.IsTrue(svc.IsUnlocked(bakery));
            Assert.AreEqual(200, _wallet.Coins.CurrentValue);
            Assert.IsTrue(changed);

            Assert.IsTrue(svc.TryUnlock(bakery)); // no double spend
            Assert.AreEqual(200, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void TryUnlock_Insufficient_DoesNotUnlockNorSpend()
        {
            _wallet.SetCoins(100);
            var svc = new BuildingUnlockService(_wallet);
            var bakery = Placeable("bakery", false, 300);

            Assert.IsFalse(svc.TryUnlock(bakery));
            Assert.IsFalse(svc.IsUnlocked(bakery));
            Assert.AreEqual(100, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void TryUnlock_ContractOnly_NotPurchasable()
        {
            _wallet.SetCoins(1000);
            var svc = new BuildingUnlockService(_wallet);
            var special = Placeable("special", false, 0); // UnlockCost 0 = contract-only

            Assert.IsFalse(svc.TryUnlock(special));
            Assert.AreEqual(1000, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void Grant_UnlocksFree_ThenTryUnlockTrueWithoutSpend()
        {
            _wallet.SetCoins(50);
            var svc = new BuildingUnlockService(_wallet);
            var special = Placeable("special", false, 0);

            svc.Grant("special");
            Assert.IsTrue(svc.IsUnlocked(special));
            // check order: already-unlocked wins over contract-only rule
            Assert.IsTrue(svc.TryUnlock(special));
            Assert.AreEqual(50, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void SaveLoad_RoundTrips_GrantedOnly()
        {
            _wallet.SetCoins(500);
            var svc = new BuildingUnlockService(_wallet);
            svc.TryUnlock(Placeable("bakery", false, 300));

            var saved = svc.ToSaveList();
            CollectionAssert.Contains(saved, "bakery");
            CollectionAssert.DoesNotContain(saved, "mill"); // default-unlocked never stored

            var restored = new BuildingUnlockService(_wallet);
            restored.LoadFrom(saved);
            Assert.IsTrue(restored.IsUnlocked(Placeable("bakery", false, 300)));
        }
    }
}
