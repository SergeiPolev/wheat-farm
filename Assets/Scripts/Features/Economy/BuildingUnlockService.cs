using System;
using System.Collections.Generic;
using System.Linq;
using WheatFarm.Core.Data;

namespace WheatFarm.Economy
{
    /// <summary>
    /// Tracks which buildings the player has unlocked. UnlockedByDefault buildings are always
    /// unlocked. A locked building is unlocked either by coin purchase (UnlockCost > 0) or by a
    /// contract reward (Grant). Mirrors DyeUnlockService.
    /// </summary>
    public interface IBuildingUnlockService
    {
        IReadOnlyCollection<string> UnlockedIds { get; }
        event Action Changed;
        bool IsUnlocked(PlaceableData placeable);
        bool TryUnlock(PlaceableData placeable);
        /// <summary>Free by-id grant (contract rewards bypass the coin cost).</summary>
        void Grant(string placeableId);
        List<string> ToSaveList();
        void LoadFrom(IEnumerable<string> ids);
    }

    public class BuildingUnlockService : IBuildingUnlockService
    {
        private readonly IWalletService _wallet;
        private readonly HashSet<string> _unlocked = new();

        public BuildingUnlockService(IWalletService wallet)
        {
            _wallet = wallet;
        }

        public IReadOnlyCollection<string> UnlockedIds => _unlocked;

        public event Action Changed;

        public bool IsUnlocked(PlaceableData placeable)
        {
            if (placeable == null) return false;
            return placeable.UnlockedByDefault || _unlocked.Contains(placeable.PlaceableId);
        }

        public bool TryUnlock(PlaceableData placeable)
        {
            if (placeable == null) return false;
            if (IsUnlocked(placeable)) return true;   // already-unlocked wins
            if (placeable.UnlockCost <= 0) return false; // contract-only

            if (!_wallet.TrySpend(placeable.UnlockCost)) return false;

            _unlocked.Add(placeable.PlaceableId);
            Changed?.Invoke();
            return true;
        }

        public void Grant(string placeableId)
        {
            if (!string.IsNullOrEmpty(placeableId) && _unlocked.Add(placeableId))
                Changed?.Invoke();
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
