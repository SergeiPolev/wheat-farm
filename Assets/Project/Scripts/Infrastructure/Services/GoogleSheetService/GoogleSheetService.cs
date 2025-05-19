using UnityEngine;
using System.Threading.Tasks;
using System;
using System.IO;
using Cathei.BakingSheet.Unity;
using Cathei.BakingSheet;
using Cathei.BakingSheet.Examples;


namespace Services
{
    public class GoogleSheetService : IService
    {
        private string _googleSheetAdress = "1qapwU4TW6L1d3tDsD2V-ztrpazm88VHYzQJ_0pjhFTc";
        protected string _currentSheetName;

        public enum ConnectionStatus
        {
            None = 0,
            Loading = 5,
            Loaded = 10,
            Error = 15,
        }

        public SheetContainer SheetContainer { get; private set; }
        public MasterConfigSheetContainer MasterConfigSheetContainer { get; private set; }
        public string BakingDate => UseJSONAsConfig ? Resources.Load<TextAsset>("bakedTime").ToString() : "Online build";

        public bool UseJSONAsConfig { get; private set; }
        public event Action<ConnectionStatus> OnStatusChanged;

        public void Initialize(StaticDataService staticDataService)
        {
            UseJSONAsConfig = staticDataService.Settings.UseJSONAsConfig;
        }

        public void SetConfigSheetId(int googleSheetId)
        {
            _googleSheetAdress = MasterConfigSheetContainer.balance[googleSheetId].google_sheet_id;
        }

        public async Task FetchConfig()
        {
            OnStatusChanged?.Invoke(ConnectionStatus.Loading);
            await BakeContainer();
        }

        public async Task FetchMasterConfig()
        {
            OnStatusChanged?.Invoke(ConnectionStatus.Loading);
            
            await BakeMasterContainer();
        }

        private async Task BakeContainer()
        {
            bool success = true;

            if (UseJSONAsConfig == false)
            {
                success = await GoogleSheetTools.ConvertFromGoogle(_googleSheetAdress);
            }
            
            if (success == false)
            {
                OnStatusChanged?.Invoke(ConnectionStatus.Error);
            }
            else
            {
                Debug.Log("Google sheet converted.");
            }
            
            ISheetConverter jsonConverter;
            jsonConverter = UseJSONAsConfig
                ? new JsonBakedConverter(PathConstants.JSONResourcesPath) 
                : new JsonSheetConverter(PathConstants.JSONFilesPath);

            var logger = UnityLogger.Default;
            var sheet = new SheetContainer(logger);
            await sheet.Bake(jsonConverter);
            sheet.Verify();
            SheetContainer = sheet;
            OnStatusChanged?.Invoke(ConnectionStatus.Loaded);
        }
        private async Task BakeMasterContainer()
        {
            await GoogleSheetTools.ConvertFromGoogleMasterConfig();

            var jsonConverter = new JsonSheetConverter(PathConstants.JSONMasterFilesPath);
            var logger = UnityLogger.Default;
            var sheet = new MasterConfigSheetContainer(logger);
            await sheet.Bake(jsonConverter);
            MasterConfigSheetContainer = sheet;
            _currentSheetName = PlayerPrefs.GetString(StringConstants.LastMasterSheetKey, MasterConfigSheetContainer.balance[0].config_id);
            OnStatusChanged?.Invoke(ConnectionStatus.Loaded);
        }
        public async Task SetMasterConfigID(string id)
        {
            PlayerPrefs.SetString(StringConstants.LastMasterSheetKey, id);
            _currentSheetName = id;
            await FetchConfig();
        }

        public async void BakeConfig(int googleSheetId)
        {
            OnStatusChanged?.Invoke(ConnectionStatus.Loading);

            _googleSheetAdress = MasterConfigSheetContainer.balance[googleSheetId].google_sheet_id;
            await BakeJSONContainer();

            await BakeContainer();
        }
        
        public async Task BakeJSONContainer()
        {
            await GoogleSheetTools.ConvertFromGoogleBake(_googleSheetAdress);

            ISheetConverter jsonConverter = new JsonBakedConverter(PathConstants.JSONResourcesPath);
            
            var logger = DebugSheetLogger.Default;
            var sheet = new SheetContainer(logger);
            await sheet.Bake(jsonConverter);
            sheet.Verify();
		
            // Create TimeStamp
            string path = Application.dataPath + "/Resources/bakedTime.txt";
            //Create File
            await File.WriteAllTextAsync(path, $"{sheet.miscellaneous.customDict["config_name"]} {DateTime.UtcNow.ToUniversalTime()} UTC");
        }

    }
}