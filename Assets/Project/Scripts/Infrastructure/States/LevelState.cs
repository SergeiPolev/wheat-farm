using Services;
using IState = StateMachine.IState;

namespace Infrastructure
{
    internal class LevelState : IState, ITick, IFixedTick, ILateTick
    {
        private IGameStateChanger _stateChanger;
        private PlayerMovementSystem _playerMovementSystem;
        private GetCropPointsService _getCropPointsService;
        private PaintFieldService _paintFieldService;
        private InputService _inputService;

        public LevelState(IGameStateChanger stateChanger, AllServices services, ICoroutineRunner coroutineRunner)
        {
            _stateChanger = stateChanger;

            _playerMovementSystem = services.Single<PlayerMovementSystem>();
            _getCropPointsService = services.Single<GetCropPointsService>();
            _paintFieldService = services.Single<PaintFieldService>();
            _inputService = services.Single<InputService>();
        }

        public void Enter()
        {
            _inputService.SetActive(true);
            _playerMovementSystem.OnEnter();
            _paintFieldService.OnLevelEnter();
            _getCropPointsService.OnLevelInit();
        }
        public void Exit()
        {       
            _inputService.SetActive(false);
        }
        
        public void Tick()
        {
            _playerMovementSystem.OnTick();
            _paintFieldService.Tick();
        }

        public void FixedTick()
        {
        }

        public void LateTick()
        {
        }
    }
}