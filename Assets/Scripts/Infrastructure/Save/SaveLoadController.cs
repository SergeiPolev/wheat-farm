using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace WheatFarm.Infrastructure.Save
{
    /// <summary>
    /// Handles save/load keybinds and auto-load on start.
    /// F5 = Save, F9 = Load. Auto-loads existing save on game start.
    /// Registered as IStartable + ITickable in FarmScope.
    /// </summary>
    public class SaveLoadController : IStartable, ITickable
    {
        private readonly IFarmSaveManager _saveManager;

        public SaveLoadController(IFarmSaveManager saveManager)
        {
            _saveManager = saveManager;
        }

        public void Start()
        {
            if (_saveManager.HasSave)
            {
                Debug.Log("[Save] Auto-loading save...");
                LoadAsync().Forget();
            }
        }

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Debug.Log("[Save] Saving...");
                SaveAsync().Forget();
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                Debug.Log("[Save] Loading...");
                LoadAsync().Forget();
            }
        }

        private async UniTaskVoid SaveAsync()
        {
            try
            {
                await _saveManager.SaveGame();
                Debug.Log("[Save] Save complete.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Save failed: {e}");
            }
        }

        private async UniTaskVoid LoadAsync()
        {
            try
            {
                await _saveManager.LoadGame();
                Debug.Log("[Save] Load complete.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Load failed: {e}");
            }
        }
    }
}
