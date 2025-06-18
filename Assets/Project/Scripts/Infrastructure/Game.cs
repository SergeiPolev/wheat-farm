using UnityEngine;
using Services;

namespace Infrastructure
{
    public class Game : MonoBehaviour, ICoroutineRunner
    {        
        private GameStateMachine _stateMachine;
        
        public GameStateMachine StateMachine => _stateMachine;

        private void Awake()
        {
            SetFPSLimit();
            SetScreenSleep();
            SetStateMachine();
        }

        private void Update()
        {
            _stateMachine.Tick();
        }

        private void FixedUpdate()
        {
            _stateMachine.FixedTick();
        }

        private void SetFPSLimit()
        {
            //QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }

        private void SetScreenSleep()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void SetStateMachine()
        {
            _stateMachine = new GameStateMachine(AllServices.Container, this);
            _stateMachine.Enter<BootstrapState>();
        }
    }
}