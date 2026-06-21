using System;
using System.Collections.Generic;
using System.Linq;
using WheatFarm.Core.Data;

namespace WheatFarm.Economy
{
    /// <summary>
    /// Tracks which dyes the player has unlocked. A dye with Cost 0 is always unlocked (e.g. White
    /// = reset). Buying a locked dye spends coins once, after which it can be used freely.
    /// Mirrors the PlantUnlockService pattern but folds the coin purchase in directly.
    /// </summary>
    public interface IDyeUnlockService
    {
        IReadOnlyCollection<string> UnlockedIds { get; }
        event Action Changed;
        bool IsUnlocked(DyeData dye);
        bool TryUnlock(DyeData dye);
        List<string> ToSaveList();
        void LoadFrom(IEnumerable<string> ids);
    }

    public class DyeUnlockService : IDyeUnlockService
    {
        private readonly IWalletService _wallet;
        private readonly HashSet<string> _unlocked = new();

        public DyeUnlockService(IWalletService wallet)
        {
            _wallet = wallet;
        }

        public IReadOnlyCollection<string> UnlockedIds => _unlocked;

        public event Action Changed;

        public bool IsUnlocked(DyeData dye)
        {
            if (dye == null) return false;
            return dye.Cost <= 0 || _unlocked.Contains(dye.DyeId);
        }

        public bool TryUnlock(DyeData dye)
        {
            if (dye == null) return false;
            if (IsUnlocked(dye)) return true;

            if (!_wallet.TrySpend(dye.Cost)) return false;

            _unlocked.Add(dye.DyeId);
            Changed?.Invoke();
            return true;
        }

        public List<string> ToSaveList() => _unlocked.ToList();

        public void LoadFrom(IEnumerable<string> ids)
        {
            _unlocked.Clear();
            if (ids != null)
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);
            Changed?.Invoke();
        }
    }
}
