using Services;
using IState = StateMachine.IState;

namespace Infrastructure
{
    internal class LevelState : IState, ITick, IFixedTick, ILateTick
    {
        private IGameStateChanger _stateChanger;

        public LevelState(IGameStateChanger stateChanger, AllServices services, ICoroutineRunner coroutineRunner)
        {
            _stateChanger = stateChanger;
        }

        public void Enter()
        {
        }
        public void Exit()
        {          
        }
        
        public void Tick()
        {
        }

        public void FixedTick()
        {
        }

        public void LateTick()
        {
        }
    }
}