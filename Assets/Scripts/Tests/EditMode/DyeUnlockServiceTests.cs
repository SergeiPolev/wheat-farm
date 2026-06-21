using NUnit.Framework;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Economy;

namespace WheatFarm.Tests
{
    public class DyeUnlockServiceTests
    {
        private WalletService _wallet;

        [SetUp]
        public void SetUp() => _wallet = new WalletService();

        [TearDown]
        public void TearDown() => _wallet.Dispose();

        private static DyeData Dye(string id, int cost)
        {
            var d = ScriptableObject.CreateInstance<DyeData>();
            d.DyeId = id;
            d.Color = Color.red;
            d.Cost = cost;
            return d;
        }

        [Test]
        public void Cost0_AlwaysUnlocked_WithoutBuying()
        {
            var svc = new DyeUnlockService(_wallet);
            Assert.IsTrue(svc.IsUnlocked(Dye("white", 0)));
        }

        [Test]
        public void TryUnlock_Affordable_SpendsAndUnlocks_AndFiresChanged()
        {
            _wallet.SetCoins(100);
            var svc = new DyeUnlockService(_wallet);
            var red = Dye("red", 20);
            bool changed = false;
            svc.Changed += () => changed = true;

            Assert.IsTrue(svc.TryUnlock(red));
            Assert.IsTrue(svc.IsUnlocked(red));
            Assert.AreEqual(80, _wallet.Coins.CurrentValue);
            Assert.IsTrue(changed);
        }

        [Test]
        public void TryUnlock_Insufficient_DoesNotUnlock_NorSpend()
        {
            _wallet.SetCoins(5);
            var svc = new DyeUnlockService(_wallet);
            var red = Dye("red", 20);

            Assert.IsFalse(svc.TryUnlock(red));
            Assert.IsFalse(svc.IsUnlocked(red));
            Assert.AreEqual(5, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void TryUnlock_AlreadyUnlocked_NoDoubleSpend()
        {
            _wallet.SetCoins(100);
            var svc = new DyeUnlockService(_wallet);
            var red = Dye("red", 20);

            Assert.IsTrue(svc.TryUnlock(red));
            Assert.IsTrue(svc.TryUnlock(red));
            Assert.AreEqual(80, _wallet.Coins.CurrentValue);
        }

        [Test]
        public void SaveLoad_RoundTrips_PurchasedOnly()
        {
            _wallet.SetCoins(100);
            var svc = new DyeUnlockService(_wallet);
            svc.TryUnlock(Dye("red", 20));

            var saved = svc.ToSaveList();
            CollectionAssert.Contains(saved, "red");
            CollectionAssert.DoesNotContain(saved, "white"); // Cost 0 is implicit, never stored

            var restored = new DyeUnlockService(_wallet);
            restored.LoadFrom(saved);
            Assert.IsTrue(restored.IsUnlocked(Dye("red", 20)));
        }
    }
}
