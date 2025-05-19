using Services;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class HUDWindow : WindowBase
{
    [System.Serializable]
    private class CurrencyPlace
    {
        public GameCurrencyID ID;
        public RectTransform Target;
    }

    public override WindowId WindowID => WindowId.LevelHud;
    private LevelProgressService _levelProgressService;
    private GoogleSheetService _googleSheetService;
    private GameWalletService _wallet;
    [SerializeField] private TMP_Text _timer;
    [SerializeField] private TMP_Text _health;
    [SerializeField] private TMP_Text _level;
    [SerializeField] private List<CurrencyPlace> _collectableTarget;
    private float _stageLength;

    public Vector3 CollectableTaget(GameCurrencyID id) =>
        _collectableTarget.Find(i => i.ID == id).Target.position;

    protected override void _Initialize(AllServices services)
    {
        _levelProgressService = services.Single<LevelProgressService>();
        _googleSheetService = services.Single<GoogleSheetService>();
        _wallet = services.Single<GameWalletService>();
    }


    protected override void _Open()
    {
        base._Open();
        var sheet = _googleSheetService.SheetContainer;
        if (sheet.GetMiscFloat("stage_length_sec", out var value))
        {
            _stageLength = value;
        }

        var lvl = _levelProgressService.CurrentLevelNumber + 1;
        var stage = _levelProgressService.CurrentStage + 1;
        _level.SetText(lvl + "-" + stage);
    }

    private void Update()
    {
        var timer = _stageLength - _levelProgressService.Timer;
        _timer.SetText(timer.ToString("F1"));
        _health.SetText(_levelProgressService.Health.ToString("F0"));
    }



}
