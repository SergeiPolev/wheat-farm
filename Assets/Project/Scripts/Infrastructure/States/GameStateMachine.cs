using StateMachine;
using System.Collections.Generic;
using Services;
using System;

namespace Infrastructure
{
    public class GameStateMachine : StateMachineBase, IGameStateChanger
    {
        public GameStateMachine(AllServices services, ICoroutineRunner coroutineRunner)
        {
            _states = new Dictionary<Type, IExitableState>()
            {
                [typeof(BootstrapState)] = new BootstrapState(this, services),
                [typeof(SelectGoogleSheetState)] = new SelectGoogleSheetState(this, services),
                [typeof(LoadGoogleSheetState)] = new LoadGoogleSheetState(this, services),
                [typeof(LoadProgressState)] = new LoadProgressState(this, services),
                [typeof(HubState)] = new HubState(this, services),
                [typeof(LoadLevelState)] = new LoadLevelState(this, services),
                [typeof(LevelState)] = new LevelState(this, services, coroutineRunner),
                [typeof(LevelResultState)] = new LevelResultState(this, services),
                [typeof(CleanupGameState)] = new CleanupGameState(this, services),
            };
        }
    }
}