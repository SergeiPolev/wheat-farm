using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Services;
using System;

public class GoogleSheetStatusWindow : WindowBase
{
    public override WindowId WindowID => WindowId.GoogleSheetStatus;

    [SerializeField] private TMP_Text _statusText;
    private GoogleSheetService _googleSheetService;

    protected override void _Initialize(AllServices services)
    {
        _googleSheetService = services.Single<GoogleSheetService>();
    }

    protected override void _Open()
    {
        base._Open();
        _statusText.SetText("");
        _googleSheetService.OnStatusChanged += SetStatus;
    }

    private void SetStatus(GoogleSheetService.ConnectionStatus status)
    {
        _statusText.SetText(status.ToString());
    }

    protected override void _Close()
    {
        base._Close();
        _googleSheetService.OnStatusChanged -= SetStatus;
    }
}
