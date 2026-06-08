using System;
using System.Collections.Generic;
using System.Linq;

namespace WheatFarm.Core.Data
{
    /// <summary>
    /// Tracks which plants are unlocked at runtime. Seeded from PlantData.UnlockedByDefault,
    /// extended via Unlock() (e.g. shop purchase), and persisted in the save file.
    /// </summary>
    public interface IPlantUnlockService
    {
        bool IsUnlocked(string plantId);
        void Unlock(string plantId);
        PlantData[] GetUnlocked();
        IReadOnlyCollection<string> UnlockedIds { get; }

        /// <summary>Raised whenever the unlocked set changes (unlock or load).</summary>
        event Action Changed;

        List<string> ToSaveList();
        void LoadFrom(IEnumerable<string> ids);
    }

    public class PlantUnlockService : IPlantUnlockService
    {
        private readonly PlantDatabase _db;
        private readonly HashSet<string> _unlocked = new();

        public event Action Changed;
        public IReadOnlyCollection<string> UnlockedIds => _unlocked;

        public PlantUnlockService(PlantDatabase db)
        {
            _db = db;
            SeedDefaults();
        }

        private void SeedDefaults()
        {
            if (_db?.Plants == null) return;
            foreach (var p in _db.Plants)
                if (p != null && p.UnlockedByDefault)
                    _unlocked.Add(p.PlantId);
        }

        public bool IsUnlocked(string plantId) =>
            !string.IsNullOrEmpty(plantId) && _unlocked.Contains(plantId);

        public void Unlock(string plantId)
        {
            if (string.IsNullOrEmpty(plantId)) return;
            if (_unlocked.Add(plantId))
                Changed?.Invoke();
        }

        public PlantData[] GetUnlocked()
        {
            if (_db?.Plants == null) return Array.Empty<PlantData>();
            return _db.Plants.Where(p => p != null && _unlocked.Contains(p.PlantId)).ToArray();
        }

        public List<string> ToSaveList() => _unlocked.ToList();

        public void LoadFrom(IEnumerable<string> ids)
        {
            _unlocked.Clear();
            SeedDefaults();
            if (ids != null)
                foreach (var id in ids)
                    if (!string.IsNullOrEmpty(id))
                        _unlocked.Add(id);
            Changed?.Invoke();
        }
    }
}
