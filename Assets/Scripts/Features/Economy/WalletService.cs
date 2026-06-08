using System;
using R3;
using UnityEngine;
using WheatFarm.Core;

namespace WheatFarm.Economy
{
    public interface IWalletService : IDisposable
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
        private readonly IDebugFlags _debug;

        public WalletService(IDebugFlags debug = null)
        {
            _debug = debug;
        }

        public ReadOnlyReactiveProperty<int> Coins => _coins;

        public bool CanAfford(int amount)
        {
            if (_debug != null && _debug.CoinsAreFree) return true;
            return _coins.Value >= amount;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            _coins.Value += amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) re