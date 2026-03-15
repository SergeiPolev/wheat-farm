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
                _saveManager.LoadGame().Forget();
            }
        }

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Debug.Log("[Save] Saving...");
                _saveManager.SaveGame().Forget();
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                Debug.Log("[Save] Loading...");
                _saveManager.LoadGame().Forget();
            }
        }
    }
}
