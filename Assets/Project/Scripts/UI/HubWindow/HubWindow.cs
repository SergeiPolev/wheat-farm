using System;
using Infrastructure;
using Services;
using UnityEngine;
using UnityEngine.UI;

public class HubWindow : WindowBase
{
    public override WindowId WindowID => WindowId.Hub;

    [SerializeField] private Button _startButton;
  
    public event Action OnGameStartEvent;

    protected override void _Open()
    {
        base._Open();
        _startButton.onClick.AddListener(ClickStartButton);
    }

    protected override void _Close()
    {
        base._Close();
        _startButton.onClick.RemoveListener(ClickStartButton);
    }

    private void ClickStartButton()
    {
       OnGameStartEvent?.Invoke();
    }
}