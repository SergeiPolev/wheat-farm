using System.Collections.Generic;
using UnityEngine;
using System;
using Data;

namespace Services
{
    [Serializable]
    public class CurrencyData 
    {
        public CurrencyId CurrencyId;
        public float Value;

        public CurrencyData(CurrencyId currencyId, float value)
        {
            CurrencyId = currencyId;
            Value = value;
        }
    }

    public class WalletService : IService, ISaveProgress
    {
        private List<CurrencyData> _currencyData;
        public event Action<CurrencyId> OnCurrencyValueChanged;
        
        public void Initialize()
        {
            _currencyData = new List<CurrencyData>
            {
                new(CurrencyId.Crystal, 0f),
                new(CurrencyId.Coins, 0f)
            };
        }

        public void UpdateProgress(PlayerProgress progress)
        {
            List<CurrencyData> currencyData = progress.WalletData.CurrencyData;

            foreach (var data in currencyData)
            {
                data.Value = GetValue(data.CurrencyId);
            }
        }

        public void LoadProgress(PlayerProgress progress)
        {
            List<CurrencyData> currencyData = progress.WalletData.CurrencyData;

            foreach (var data in currencyData)
            {
                SetValue(data.CurrencyId, data.Value);
            }
        }

        public void AddValue(CurrencyId currencyId, float value)
        {
            if (value <= 0)
            {
                Debug.Log($"Incorrect add {currencyId} value {value}");
                return;
            }

            AddValue(ref GetCurrencyValue(currencyId), value);
            OnCurrencyValueChanged?.Invoke(currencyId);
        }

        public void SpendValue(CurrencyId currencyId, float value)
        {
            if (value <= 0)
            {
                Debug.Log($"Incorrect add {currencyId} value {value}");
                return;
            }

            
            SpendValue(ref GetCurrencyValue(currencyId), value);
            OnCurrencyValueChanged?.Invoke(currencyId);
        }

        public int GetValue(CurrencyId resourceType)
        {
            return (int)GetCurrencyValue(resourceType);

        }
        public bool IsEnough(CurrencyId resourceType, float value)
        {
            return value <= GetValue(resourceType);
        }

        private void AddValue(ref float resourceValue, float value)
        {
            resourceValue += value;
        }

        private void SpendValue(ref float resourceValue, float value)
        {
            if (resourceValue < value)
            {
                Debug.LogError($"Incorrect resource value {resourceValue}");
                return;
            }

            resourceValue -= value;
        }

        public void SetValue(CurrencyId currencyId, float value)
        {
            GetCurrencyValue(currencyId) = value;
            OnCurrencyValueChanged?.Invoke(currencyId);
        }

        private ref float GetCurrencyValue(CurrencyId currencyId)
        {
            CurrencyData currencyData = _currencyData.Find(x => x.CurrencyId == currencyId);

            if (currencyData != null)
            {
                return ref currencyData.Value;
            }

            throw new Exception($"Incorrect resource type {currencyId}");
        }
    }
}