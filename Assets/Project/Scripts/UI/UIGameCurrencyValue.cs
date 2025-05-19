using Services;
using TMPro;
using UnityEngine;

public class UIGameCurrencyValue : MonoBehaviour
{
    [SerializeField] private GameCurrencyID _id;
    [SerializeField] private TMP_Text _value;
    private GameWalletService _wallet;

    private void OnEnable()
    {
        if(_wallet == null)
            _wallet = AllServices.Container.Single<GameWalletService>();

        _wallet.OnCurrencyValueChanged += _wallet_OnCurrencyValueChanged;
        UpdateValue();
    }

    private void OnDisable()
    {
        _wallet.OnCurrencyValueChanged -= _wallet_OnCurrencyValueChanged;
    }

    private void _wallet_OnCurrencyValueChanged(GameCurrencyID id)
    {
        if(id == _id)
            UpdateValue();
    }

    private void UpdateValue()
    {
        _value.SetText(_wallet.GetValue(_id).ToString("F0"));
    }
}