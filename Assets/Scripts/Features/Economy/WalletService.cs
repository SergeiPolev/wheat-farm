using R3;
using UnityEngine;

namespace WheatFarm.Economy
{
    public interface IWalletService
    {
        ReadOnlyReactiveProperty<int> Coins { get; }
        bool CanAfford(int amount);
        void Add(int amount);
        bool TrySpend(int amount);
        void SetCoins(int amount);
    }

    public class WalletService : IWalletService
    {
        private readonly ReactiveProperty<int> _coins = new(100); // starter money
        public ReadOnlyReactiveProperty<int> Coins => _coins;

        public bool CanAfford(int amount) => _coins.Value >= amount;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            _coins.Value += amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || !CanAfford(amount)) return false;
            _coins.Value -= amount;
            return true;
        }

        public void SetCoins(int amount) => _coins.Value = Mathf.Max(0, amount);
    }
}
