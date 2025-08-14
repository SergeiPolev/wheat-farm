using StateMachine;
using Services;

namespace Infrastructure
{
    public class BootstrapState : IState
    {
        private IGameStateChanger _stateChanger;
        private AllServices _services;
        private GoogleSheetService _googleSheetService;

        public BootstrapState(IGameStateChanger stateChanger, AllServices services)
        {
            _stateChanger = stateChanger;
            _services = services;
            RegisterServices();
            InitServices();
            _googleSheetService = services.Single<GoogleSheetService>();
        }

        public void Enter()
        {
            _stateChanger.Enter<LevelState>();
            /*if (_googleSheetService.UseJSONAsConfig)
            {
                _stateChanger.Enter<LoadGoogleSheetState>();
            }
            else
            {
                _stateChanger.Enter<SelectGoogleSheetState>();
            }*/
        }

        private void RegisterServices()
        {
            _services.RegisterSingle(new GoogleSheetService());
            _services.RegisterSingle(new StaticDataService());
            _services.RegisterSingle(new SaveLoadService());
            _services.RegisterSingle(new PersistentProgressService());
            _services.RegisterSingle(new LevelProgressService());
            _services.RegisterSingle(new WalletService());
            _services.RegisterSingle(new DebugService());
            _services.RegisterSingle(new GameWalletService());
            _services.RegisterSingle(new PlayerMovementSystem());
            _services.RegisterSingle(new GetCropPointsService());
            _services.RegisterSingle(new FieldToolsService());
            _services.RegisterSingle(new ChangeToolsService());

            _services.RegisterSingle(new UIFactory());
            _services.RegisterSingle(new WindowService());
            _services.RegisterSingle(new CombatTextFactory());

            _services.RegisterSingle(new InputService());
            _services.RegisterSingle(new PauseService());
            _services.RegisterSingle(new GlobalBlackboard());

            _services.RegisterSingle(new GameFactory());
            _services.RegisterSingle(new CameraService());

        }

        private void InitServices()
        {
            _services.Single<StaticDataService>().Initialize();
            _services.Single<WalletService>().Initialize();
            _services.Single<GameWalletService>().Initialize();
            _services.Single<LevelProgressService>().Initialize(_services);
            _services.Single<UIFactory>().Initialize(_services);
            _services.Single<SaveLoadService>().Initialize(_services);
            _services.Single<WindowService>().Initialize(_services.Single<UIFactory>());
            _services.Single<InputService>().Initialize(_services.Single<WindowService>());
            _services.Single<GoogleSheetService>().Initialize(_services.Single<StaticDataService>());
            _services.Single<GlobalBlackboard>().Initialize();
            _services.Single<CombatTextFactory>().Initialize();
            _services.Single<ChangeToolsService>().Initialize();

            _services.Single<CameraService>().Initialize();
            _services.Single<GameFactory>()
                .Initialize(_services.Single<GlobalBlackboard>(), _services.Single<StaticDataService>());
        }


        public void Exit()
        {

        }
    }
}