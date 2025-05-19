using Data;
using Services;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Services
{
    public class SaveLoadService: IService
    {
        private ISaveSystem _saveSystem;
        private PersistentProgressService _progressService;
        private AllServices _services;

        public void Initialize(AllServices services)
        {
            _services = services;
            _progressService = services.Single<PersistentProgressService>();
            _saveSystem = new PlayerPrefsJsonSaveSystem();
        }

        public void SaveProgress()
        {
            foreach (ISaveProgress progressWriter in _services.ProgressWirters)
            {
                progressWriter.UpdateProgress(_progressService.Progress);
            }

            _saveSystem.Save(_progressService.Progress);
        }

        public PlayerProgress LoadProgress()
        {
            
            if (_saveSystem.HasData())
            {
                var data = _saveSystem.Load();
                if (data.WalletData == null)
                    data.WalletData = new WalletData();
                return data;
            }

            return null;
        }
    }
}