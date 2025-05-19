using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using Services;
using System;

namespace Infrastructure
{
    internal class HubState : IState
    {
        private WindowService _windowService;
        private IGameStateChanger _stateChanger;
        private FadeWindowModel _fadeWindowModel;

        private HubWindow _uiHub;
        private TransitionFadeWindow _uiFade;

        public HubState(IGameStateChanger stateChanger, AllServices services) 
        {
            _stateChanger = stateChanger;
            _windowService = services.Single<WindowService>();
            _fadeWindowModel = new FadeWindowModel(0f, 0.5f, 0.1f, 0.5f, SetState);
        }

        public void Enter()
        {
            _windowService.Open(WindowId.Hub, out _uiHub);
            _windowService.Open(WindowId.HubDebug);
            _uiHub.OnGameStartEvent += Window_OnGameStartEvent;
        }

        private void Window_OnGameStartEvent()
        {
            _uiHub.OnGameStartEvent -= Window_OnGameStartEvent;
            _windowService.Open(WindowId.TransitionFade, out _uiFade);
            _uiFade.SetModel(_fadeWindowModel);
        }


        private void SetState()
        {
            _stateChanger.Enter<LoadLevelState>();
            _windowService.Close(WindowId.Hub);
            _windowService.Close(WindowId.HubDebug);
        }



        public void Exit()
        {
            
        }
    }
}

