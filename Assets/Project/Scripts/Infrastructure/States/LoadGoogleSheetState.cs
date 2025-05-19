using Services;
using StateMachine;
using System.Threading.Tasks;

namespace Infrastructure
{
    public class LoadGoogleSheetState :  IState
    {
        private IGameStateChanger _stateChanger;
        private GoogleSheetService _googleSheetService;
        private StaticDataService _staticData;
        
        protected WindowService _windowService;

        private SheetContainer SheetContainer => _googleSheetService.SheetContainer;
        
        public LoadGoogleSheetState(IGameStateChanger stateChanger, AllServices services) 
        {
            _stateChanger = stateChanger;
            _googleSheetService = services.Single<GoogleSheetService>();
            _staticData = services.Single<StaticDataService>();
            _windowService = services.Single<WindowService>();
        }


        public async void Enter()
        {
            _googleSheetService.OnStatusChanged += CheckGoogleSheetLoadingStatus;

            _windowService.Open(WindowId.GoogleSheetStatus);
            await _googleSheetService.FetchConfig();
        }


        private void CheckGoogleSheetLoadingStatus(GoogleSheetService.ConnectionStatus status)
        {
            if (status == GoogleSheetService.ConnectionStatus.Loaded)
            {
                ApplyConfigs();
            }
        }
        private async void ApplyConfigs()
        {
            // Apply any configs that depends on online config or JSON config here
            
            await Task.Delay(100);
            
            _stateChanger.Enter<LoadProgressState>();
        }

        public void Exit()
        {
            _googleSheetService.OnStatusChanged -= CheckGoogleSheetLoadingStatus;

            _windowService.Close(WindowId.GoogleSheetStatus);
        }
    }
}