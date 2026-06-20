using R3;
using WheatFarm.Core;
using WheatFarm.Economy;

namespace WheatFarm.Infrastructure.Cheats
{
    /// <summary>
    /// Debug decorator over IWalletService. When coins are free, spends succeed without
    /// deducting. Forwards everything else. The Economy assembly has NO debug dependency.
    /// </summary>
    public class DebugWalletService : IWalletService
    {
        private readonly IWalletService _inner;
        private readonly IDebugFlags _debug;

        public DebugWalletService(WalletService inner, IDebugFlags debug)
        {
            _inner = inner;
            _debug = debug;
        }

        public ReadOnlyReactiveProperty<int> Coins => _inner.Coins;

        public bool CanAfford(int amount) => _debug.CoinsAreFree || _inner.CanAfford(amount);

        public void Add(int amount) => _inner.Add(amount);

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return false;
            if (_debug.CoinsAreFree) return true; // free
            return _inner.TrySpend(amount);
        }

        public void SetCoins(int amount) => _inner.SetCoins(amount);

        public void Dispose() => _inner.Dispose();
    }
}
