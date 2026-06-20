using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WheatFarm.Infrastructure.Save
{
    /// <summary>
    /// Handles reading/writing FarmSaveData to JSON file on disk.
    /// Registered in RootScope — persists across game sessions.
    /// Actual data collection/restore is done by FarmScope (Phase 11).
    /// </summary>
    public interface IFarmSaveService
    {
        UniTask Save(FarmSaveData data);
        UniTask<FarmSaveData> Load();
        /// <summary>
        /// Returns true only if a save file exists AND its Version >= 2 (save format v2+).
        /// If a save exists but is incompatible (v1) or corrupt, the file is deleted and false is returned.
        /// Result is cached for the session lifetime.
        /// </summary>
        bool HasSave();
        void DeleteSave();
    }

    public class FarmSaveService : IFarmSaveService
    {
        private const string FileName = "farm_save.json";
        private const int MinCompatibleVersion = 2;

        protected virtual string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// Cached probe result. null = not yet probed this session.
        /// </summary>
        private bool? _hasCompatibleSaveCache;

        public async UniTask Save(FarmSaveData data)
        {
            var json = JsonUtility.ToJson(data, prettyPrint: true);
            var dir = Path.GetDirectoryName(SavePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(SavePath, json);
            // Invalidate probe cache after a fresh save (the new save is always compatible).
            _hasCompatibleSaveCache = true;
            Debug.Log($"[FarmSaveService] Saved to {SavePath}");
        }

        public async UniTask<FarmSaveData> Load()
        {
            if (!HasSave())
            {
                Debug.LogWarning("[FarmSaveService] No save file found or save is incompatible, returning default data");
                return new FarmSaveData();
            }

            var json = await File.ReadAllTextAsync(SavePath);
            var data = JsonUtility.FromJson<FarmSaveData>(json);

            // Defensive version guard on the load path as well.
            if (data == null || data.Version < MinCompatibleVersion)
            {
                Debug.LogWarning($"[FarmSaveService] Load: save version {data?.Version ?? 0} < {MinCompatibleVersion}, deleting and returning default.");
                DeleteSave();
                return new FarmSaveData();
            }

            Debug.Log($"[FarmSaveService] Loaded from {SavePath}");
            return data;
        }

        /// <summary>
        /// Returns true only when a compatible (Version >= 2) save exists on disk.
        /// On first call, reads and deserializes the file to check the version.
        /// If the file is v1 or corrupt, deletes it and returns false (no retry this session).
        /// </summary>
        public bool HasSave()
        {
            if (_hasCompatibleSaveCache.HasValue)
                return _hasCompatibleSaveCache.Value;

            if (!File.Exists(SavePath))
            {
                _hasCompatibleSaveCache = false;
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(SavePath);
            }
            catch (System.Exception ex)
            {
                // Transient I/O failure (file locked by cloud sync / AV, etc.) — do NOT delete a
                // potentially-valid save. Report no-compatible-save for this session; retry next launch.
                Debug.LogWarning($"[FarmSaveService] Could not read save file (treating as no save, NOT deleting). Error: {ex.Message}");
                _hasCompatibleSaveCache = false;
                return false;
            }

            FarmSaveData data;
            try
            {
                data = JsonUtility.FromJson<FarmSaveData>(json);
            }
            catch (System.Exception ex)
            {
                // The bytes read fine but aren't valid save JSON — genuinely corrupt; delete and start fresh.
                Debug.LogWarning($"[FarmSaveService] Save file is corrupt, deleting. Error: {ex.Message}");
                try { File.Delete(SavePath); } catch { /* best effort */ }
                _hasCompatibleSaveCache = false;
                return false;
            }

            if (data != null && data.Version >= MinCompatibleVersion)
            {
                _hasCompatibleSaveCache = true;
                return true;
            }

            // Parsed cleanly but is an old/incompatible version — delete and start fresh.
            Debug.LogWarning($"[FarmSaveService] Save version {data?.Version ?? 0} < {MinCompatibleVersion} (requires v{MinCompatibleVersion}+). Deleting incompatible save.");
            try { File.Delete(SavePath); } catch { /* best effort */ }
            _hasCompatibleSaveCache = false;
            return false;
        }

        public void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                _hasCompatibleSaveCache = false;
                Debug.Log($"[FarmSaveService] Deleted save at {SavePath}");
            }
        }
    }
}
