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
        bool HasSave();
        void DeleteSave();
    }

    public class FarmSaveService : IFarmSaveService
    {
        private const string FileName = "farm_save.json";

        private string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public async UniTask Save(FarmSaveData data)
        {
            var json = JsonUtility.ToJson(data, prettyPrint: true);
            var dir = Path.GetDirectoryName(SavePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(SavePath, json);
            Debug.Log($"[FarmSaveService] Saved to {SavePath}");
        }

        public async UniTask<FarmSaveData> Load()
        {
            if (!HasSave())
            {
                Debug.LogWarning("[FarmSaveService] No save file found, returning default data");
                return new FarmSaveData();
            }

            var json = await File.ReadAllTextAsync(SavePath);
            var data = JsonUtility.FromJson<FarmSaveData>(json);
            Debug.Log($"[FarmSaveService] Loaded from {SavePath}");
            return data;
        }

        public bool HasSave() => File.Exists(SavePath);

        public void DeleteSave()
        {
            if (HasSave())
            {
                File.Delete(SavePath);
                Debug.Log($"[FarmSaveService] Deleted save at {SavePath}");
            }
        }
    }
}
