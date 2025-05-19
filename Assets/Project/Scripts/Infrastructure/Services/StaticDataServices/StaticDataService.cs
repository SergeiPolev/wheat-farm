using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Services
{
    public class StaticDataService : IService
    {
        private const string StaticDataWindowsPath = "StaticData/Window/WindowStaticData";
        private const string SettingsPath = "StaticData/Settings/SettingsStaticData";
        private const string RarityPath = "StaticData/Settings/RarityStaticData";
        private string _vfxStaticDataPath = "StaticData/VFX/VFXStaticData";
        private string _prefabStaticDataPath = "StaticData/Prefabs/PrefabStaticData";
        private string _materialsStaticDataPath = "StaticData/Materials/MaterialsStaticData";

        private Dictionary<WindowId, WindowBase> _windowConfigs;
        public SettingsStaticData Settings { get; set; }
        public PrefabStaticData Prefabs { get; set; }
        public MaterialsStaticData Materials { get; set; }

        public VFXStaticData VfxStaticData;

        public void Initialize()
        {
            LoadWindows();
            LoadSettings();
            LoadPrefabs();
            LoadMaterials();
            LoadLevels();
            LoadVFX();
        }

        private void LoadMaterials()
        {
            Materials = Resources.Load<MaterialsStaticData>(_materialsStaticDataPath);
        }

        private void LoadPrefabs()
        {
            Prefabs = Resources.Load<PrefabStaticData>(_prefabStaticDataPath);
        }
        
        public WindowBase ForWindow(WindowId windowId)
        {
            if (_windowConfigs.TryGetValue(windowId, out var windowConfig))
            {
                return windowConfig;
            }

            throw new Exception($"Error! Dont found static data of type {windowId}");
        }


        private void LoadSettings()
        {
            Settings = Resources.Load<SettingsStaticData>(SettingsPath);
        }

        private void LoadWindows()
        {
            _windowConfigs = Resources.Load<WindowStaticData>(StaticDataWindowsPath)
                .Configs
                .ToDictionary(x => x.WindowID, x => x);
        }

        private void LoadVFX()
        {
            VfxStaticData = Resources.Load<VFXStaticData>(_vfxStaticDataPath);
        }
        private void LoadLevels()
        {
        }
    }
}