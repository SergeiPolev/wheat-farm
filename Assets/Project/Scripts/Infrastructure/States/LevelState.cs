using Services;
using IState = StateMachine.IState;

namespace Infrastructure
{
    internal class LevelState : IState, ITick, IFixedTick, ILateTick
    {
        private IGameStateChanger _stateChanger;
        private PlayerMovementSystem _playerMovementSystem;
        private GetCropPointsService _getCropPointsService;
        private FieldToolsService _fieldToolsService;
        private ChangeToolsService _changeToolsService;
        private InputService _inputService;

        public LevelState(IGameStateChanger stateChanger, AllServices services, ICoroutineRunner coroutineRunner)
        {
            _stateChanger = stateChanger;

            _playerMovementSystem = services.Single<PlayerMovementSystem>();
            _getCropPointsService = services.Single<GetCropPointsService>();
            _fieldToolsService = services.Single<FieldToolsService>();
            _changeToolsService = services.Single<ChangeToolsService>();
            _inputService = services.Single<InputService>();
        }

        public void Enter()
        {
            _inputService.SetActive(true);
            _playerMovementSystem.OnEnter();
            _fieldToolsService.OnLevelEnter();
            _getCropPointsService.OnLevelInit();
        }
        public void Exit()
        {       
            _inputService.SetActive(false);
        }
        
        public void Tick()
        {
            _playerMovementSystem.OnTick();
            _fieldToolsService.Tick();
            _changeToolsService.Tick();
        }

        public void FixedTick()
        {
        }

        public void LateTick()
        {
        }
    }
}