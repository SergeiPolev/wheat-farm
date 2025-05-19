using Services;
using StateMachine;

namespace Infrastructure
{
    public class SelectGoogleSheetState :  IState
    {
        private IGameStateChanger _stateChanger;
        private GoogleSheetService _googleSheetService;
        protected WindowService _windowService;


        public SelectGoogleSheetState(IGameStateChanger stateChanger, AllServices services) 
        {
            _stateChanger = stateChanger;
            _googleSheetService = services.Single<GoogleSheetService>();
            _windowService = services.Single<WindowService>();
        }

        public async void Enter()
        {
            _windowService.Open(WindowId.GoogleSheetConfig, out GoogleSheetConfigWindow window);
            window.OnCloseE += Window_OnCloseE;
            await _googleSheetService.FetchMasterConfig();
        }

        private void Window_OnCloseE(WindowBase window)
        {
            window.OnCloseE -= Window_OnCloseE;
            _stateChanger.Enter<LoadGoogleSheetState>();
        }


        public void Exit()
        {

        }
    }
}