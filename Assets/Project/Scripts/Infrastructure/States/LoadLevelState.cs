using StateMachine;
using Services;

namespace Infrastructure
{
    internal class LoadLevelState : IState
    {
        private IGameStateChanger _stateChanger;
        private PersistentProgressService _gameProgress;
        private InputService _inputService;
        private LevelProgressService _levelProgress;
        private GameWalletService _wallet;
        private GoogleSheetService _googleSheetService;

        public LoadLevelState(IGameStateChanger stateChanger, AllServices services) 
        {
            _stateChanger = stateChanger;
            _gameProgress = services.Single<PersistentProgressService>();
            _inputService = services.Single<InputService>();
            _levelProgress = services.Single<LevelProgressService>();
            _wallet = services.Single<GameWalletService>();
            _googleSheetService = services.Single<GoogleSheetService>();
        }

        public void Enter()
        {
            _wallet.LoadLevel();
            _levelProgress.ResetTimer();
            _stateChanger.Enter<LevelState>();
        }

        public void Exit()
        {

        }

    }
}