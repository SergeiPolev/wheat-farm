using Services;
using IState = StateMachine.IState;

namespace Infrastructure
{
    internal class LevelState : IState, ITick, IFixedTick, ILateTick
    {
        private IGameStateChanger _stateChanger;
        private PlayerMovementSystem _playerMovementSystem;
        private InputService _inputService;

        public LevelState(IGameStateChanger stateChanger, AllServices services, ICoroutineRunner coroutineRunner)
        {
            _stateChanger = stateChanger;

            _playerMovementSystem = services.Single<PlayerMovementSystem>();
            _inputService = services.Single<InputService>();
        }

        public void Enter()
        {
            _inputService.SetActive(true);
            _playerMovementSystem.OnEnter();
        }
        public void Exit()
        {       
            _inputService.SetActive(false);
        }
        
        public void Tick()
        {
            _playerMovementSystem.OnTick();
        }

        public void FixedTick()
        {
        }

        public void LateTick()
        {
        }
    }
}