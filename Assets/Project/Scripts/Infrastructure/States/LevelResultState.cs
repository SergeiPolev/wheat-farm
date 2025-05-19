using StateMachine;
using Services;

namespace Infrastructure
{
    internal class LevelResultState : ITick, IPayloadedState<string>
    {
        private WindowService _windowService;
        private IGameStateChanger _stateChanger;
        private PauseService _pauseService;
        protected string _result;

        public LevelResultState(IGameStateChanger stateChanger, AllServices services) 
        {
            _stateChanger = stateChanger;
            _pauseService = services.Single<PauseService>();
            _windowService = services.Single<WindowService>();
        }

        public void Tick()
        {
            
        }

        public void Enter(string result)
        {
            //DOVirtual.DelayedCall(isWin ? 2f : 2.5f, SetResult);
            _stateChanger.Enter<CleanupGameState>();
        }

        public void Exit()
        {

        }
    }
}