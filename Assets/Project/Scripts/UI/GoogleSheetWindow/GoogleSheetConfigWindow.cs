using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Services;
using UnityEngine.UI;

public class GoogleSheetConfigWindow : WindowBase
{
    public override WindowId WindowID =>  WindowId.GoogleSheetConfig;

    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Dropdown _dropdown;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _bakeConfig;

    private List<TMP_Dropdown.OptionData> _optionsData;
    private GoogleSheetService _googleSheetService;

    protected override void _Initialize(AllServices services)
    {
        _googleSheetService = services.Single<GoogleSheetService>();
        _optionsData = new List<TMP_Dropdown.OptionData>();
    }

    protected override void _Open()
    {
        base._Open();
        _googleSheetService.OnStatusChanged += SetConfigList;
        _bakeConfig.onClick.AddListener(BakeConfig);
        _confirmButton.onClick.AddListener(SelectConfig);
        RefreshWindow();
    }

    protected override void _Close()
    {
        base._Close();
        _googleSheetService.OnStatusChanged -= SetConfigList;
        _confirmButton.onClick.RemoveListener(SelectConfig);
        _bakeConfig.onClick.RemoveListener(BakeConfig);
    }

    private void SelectConfig()
    {
        _googleSheetService.SetConfigSheetId(_dropdown.value);
        Close();
    }
    
    private void BakeConfig()
    {
        _googleSheetService.BakeConfig(_dropdown.value);
        Close();
    }

    protected void RefreshWindow()
    {
        _statusText.SetText("");
        _confirmButton.gameObject.SetActive(false);
        _dropdown.gameObject.SetActive(false);
        
#if !UNITY_EDITOR
        _bakeConfig.gameObject.SetActive(false);
#else
        _bakeConfig.gameObject.SetActive(true);
#endif
    }

    private void SetConfigList(GoogleSheetService.ConnectionStatus status)
    {
        if (status == GoogleSheetService.ConnectionStatus.Loaded)
        {
            _dropdown.gameObject.SetActive(true);
            SetOptionsData();
            _dropdown.AddOptions(_optionsData);
            _statusText.SetText("Select config");
            _confirmButton.gameObject.SetActive(true);
        }
        else
        {
            _statusText.SetText(status.ToString());
        }
    }

    private void SetOptionsData()
    {
        _dropdown.ClearOptions();
        _optionsData.Clear();

        foreach (MasterConfigSheet.BalanceConfig balanceConfig in _googleSheetService.MasterConfigSheetContainer.balance)
        {
            TMP_Dropdown.OptionData optionData = new TMP_Dropdown.OptionData(balanceConfig.config_id);
            _optionsData.Add(optionData);
        }
    }

}
