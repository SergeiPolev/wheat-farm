using StateMachine;
using Services;
using Data;

namespace Infrastructure
{
    internal class LoadProgressState : IState
    {
        private PersistentProgressService _progressService;
        private SaveLoadService _saveLoadService;
        private IGameStateChanger _stateChanger;
        private WindowService _windowService;
        //private TutorialService _tutorialService;
        private AllServices _services;

        public LoadProgressState(IGameStateChanger stateChanger, AllServices services) 
        {
            _stateChanger = stateChanger;
            _progressService = services.Single<PersistentProgressService>();
            _saveLoadService = services.Single<SaveLoadService>();
            _windowService = services.Single<WindowService>();
            //_tutorialService = services.Single<TutorialService>();
            _services = services;
        }

        public void Enter()
        {
            LoadProgressOrInitNew();
            InformPrgressReaders();
            SetState();
        }


        private void SetState()
        {
            _stateChanger.Enter<HubState>();
        }

        private void LoadProgressOrInitNew()
        {
            _progressService.Progress = _saveLoadService.LoadProgress() ?? NewProgress();
        }

        private PlayerProgress NewProgress()
        {
            return new PlayerProgress();
        }

        private void InformPrgressReaders()
        {
            foreach (ISavedProgressReader progressReader in _services.ProgressReaders)
            {
                progressReader.LoadProgress(_services.Single<PersistentProgressService>().Progress);
            }
        }

        public void Exit()
        {

        }
    }
}