using Data;
using System;
using UnityEngine;

namespace Services
{
    public class LevelProgressService : IService, ISaveProgress, ITick
    {
        private float _timer;
        private SaveLoadService _saveLoadService;
        private GoogleSheetService _googleSheetService;
        private bool _isPause;
        public float Timer => _timer;
        public int LevelIndex => CurrentLevelNumber - 1;
        public bool IsMaxLevel => CurrentLevelNumber == MaxLevel;
        public int CurrentStage { get; private set; }
        public int CurrentLevelNumber { get; private set; }
        public int MaxLevel { get; private set; }
        public float LevelProgress { get; set; }
        public bool IsDiggering { get; private set; }

        public int Health { get; private set; }
        
        public void Initialize(AllServices services )
        {
            _saveLoadService = services.Single<SaveLoadService>();
            _googleSheetService = services.Single<GoogleSheetService>();
        }

        public void UpdateProgress(PlayerProgress progress)
        {
            //progress.LevelProgressData.MaxLevel = MaxLevel;
        }

        public void LoadProgress(PlayerProgress progress)
        {
            //MaxLevel = progress.LevelProgressData.MaxLevel;
            //CurrentLevelNumber = MaxLevel;
        }

        
        public void PauseTimer(bool isPause)
        {
            _isPause = isPause;
        }

        public void Tick()
        {
            if (_isPause)
            {
                return;
            }

            _timer += Time.deltaTime;
        }




        #region Level

        public void LevelStarted()
        {
            if (_googleSheetService.SheetContainer.GetMiscFloat("stage_mode_hp", out var value))
                Health = Mathf.RoundToInt(value);
            else
                Health = 1;

            CurrentStage = 0;
            _timer = 0;
            CurrentStage = 0;
        }

        public void LevelComplete()
        {
            if (IsMaxLevel)
            {
                MaxLevel++;
                _saveLoadService.SaveProgress();
            }

            CurrentStage = 0;
            LevelProgress = 1;
            SetNextLevel();
        }

        public void SetNextLevel()
        {
            if (IsMaxLevel == false)
            {
                CurrentLevelNumber++;
            }
        }
        public void SetPreviousLevel()
        {
            if (CurrentLevelNumber > 0)
            {
                CurrentLevelNumber--;
            }
        }

        #endregion

        #region Stage

        public void StartStage()
        {
            IsDiggering = true;
            ResetTimer();
        }

        public void ResetTimer()
        {
            _timer = 0;
            _isPause = false;
        }

        public void StagePassed()
        {
            CurrentStage++;
            IsDiggering = false;
        }


        internal void LoseStage()
        {
            Health -= 1;
            IsDiggering = false;
        }

        #endregion
    }
}