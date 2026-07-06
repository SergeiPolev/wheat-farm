using System;
using System.Collections.Generic;
using System.Linq;
using ObservableCollections;
using R3;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.DayNight;

namespace WheatFarm.Economy
{
    /// <summary>
    /// Rebuilds the *available* contract set each Dawn from the eligible pool:
    /// contracts whose reward isn't already unlocked, whose requirements are
    /// obtainable (no items from locked plants), and which aren't already active.
    /// The set + day index persist across save/load; loading suppresses the
    /// initial-phase rotation so a restored board isn't immediately reshuffled.
    /// </summary>
    public class ContractRotationService : IStartable, IDisposable
    {
        /// <summary>Contracts offered per day.</summary>
        public const int DailyCount = 4;

        private readonly ContractDatabase _db;
        private readonly IDayNightService _dayNight;
        private readonly IPlantUnlockService _plants;
        private readonly IDyeUnlockService _dyes;
        private readonly IContractService _contracts;
        private readonly PlantDatabase _plantDb;
        private IDisposable _subscription;
        private bool _sawFirstPhase; // ReadOnlyReactiveProperty replays current value on subscribe

        public ObservableList<ContractData> Available { get; } = new();
        public int DayIndex { get; private set; }

        public ContractRotationService(
            ContractDatabase db,
            IDayNightService dayNight,
            IPlantUnlockService plants,
            IDyeUnlockService dyes,
            IContractService contracts,
            PlantDatabase plantDb)
        {
            _db = db;
            _dayNight = dayNight;
            _plants = plants;
            _dyes = dyes;
            _contracts = contracts;
            _plantDb = plantDb;
        }

        public void Start()
        {
            // Fresh session without a save: fill the board once
            if (Available.Count == 0)
                Rotate();

            if (_dayNight != null)
            {
                _subscription = _dayNight.CurrentPhase.Subscribe(phase =>
                {
                    // Skip the replayed current value — only genuine transitions into Dawn rotate
                    if (!_sawFirstPhase)
                    {
                        _sawFirstPhase = true;
                        return;
                    }
                    if (phase == TimeOfDay.Dawn)
                        Rotate();
                });
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        /// <summary>Deterministic pick of at most n eligible contracts (seeded shuffle).</summary>
        public IReadOnlyList<ContractData> SelectEligible(int n, int seed)
        {
            var eligible = new List<ContractData>();
            if (_db?.Contracts != null)
                foreach (var c in _db.Contracts)
                    if (c != null && IsEligible(c))
                        eligible.Add(c);

            var rng = new Random(seed);
            for (int i = eligible.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
            }

            if (eligible.Count > n)
                eligible.RemoveRange(n, eligible.Count - n);
            return eligible;
        }

        public void Rotate()
        {
            DayIndex++;
            Available.Clear();
            foreach (var c in SelectEligible(DailyCount, DayIndex))
                Available.Add(c);
        }

        public (List<string> ids, int dayIndex) ToSave() =>
            (Available.Select(c => c.ContractId).ToList(), DayIndex);

        public void LoadFrom(IEnumerable<string> ids, int dayIndex)
        {
            DayIndex = dayIndex;
            Available.Clear();
            if (ids == null) return;
            foreach (var id in ids)
            {
                var contract = _db?.GetById(id);
                if (contract != null)
                    Available.Add(contract);
            }
        }

        private bool IsEligible(ContractData c)
        {
            // Reward already owned → pointless offer
            if (!string.IsNullOrEmpty(c.UnlockPlantId) && _plants.IsUnlocked(c.UnlockPlantId))
                return false;
            if (!string.IsNullOrEmpty(c.UnlockDyeId) && _dyes.UnlockedIds.Contains(c.UnlockDyeId))
                return false;

            // Already on the active board
            for (int i = 0; i < _contracts.ActiveContracts.Count; i++)
                if (_contracts.ActiveContracts[i].Data.ContractId == c.ContractId)
                    return false;

            // Requirements must be obtainable: an item that IS a plant must be unlocked.
            // Non-plant items (produced goods, wood, …) are always considered obtainable.
            if (c.Required != null)
                foreach (var req in c.Required)
                    if (_plantDb?.GetById(req.ItemId) != null && !_plants.IsUnlocked(req.ItemId))
                        return false;

            return true;
        }
    }
}
