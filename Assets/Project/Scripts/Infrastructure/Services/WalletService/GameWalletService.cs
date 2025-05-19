using System.Collections.Generic;
using System;
using UnityEngine;

namespace Services
{
    public class GameWalletService : IService
    {
        private Dictionary<GameCurrencyID, float> _currencyData;
        
        public event Action<GameCurrencyID> OnCurrencyValueChanged;

        public void Initialize()
        {
            _currencyData = new Dictionary<GameCurrencyID, float>();
        }

        public void LoadLevel()
        {
            _currencyData.Clear();
            _currencyData.Add(GameCurrencyID.Gold, 0);
        }
        
        public void SpendValue(GameCurrencyID currencyId, float value)
        {
            if (value <= 0)
            {
                Debug.Log($"Incorrect add {currencyId} value {value}");
                return;
            }

            _currencyData[currencyId] -= value;
            OnCurrencyValueChanged?.Invoke(currencyId);
        }
        
        public void AddValue(GameCurrencyID currencyId, float value)
        {
            if (_currencyData.ContainsKey(currencyId))
            {
                _currencyData[currencyId] += value;
            }
            else
            {
                Debug.LogWarning($"Incorrect resource type {currencyId}");
                _currencyData.Add(currencyId, value);
            }
            OnCurrencyValueChanged?.Invoke(currencyId);
        }

        public float GetValue(GameCurrencyID currencyId)
        {     
            if (_currencyData.ContainsKey(currencyId))
            {
                return _currencyData[currencyId];
            }
            else
            {
                Debug.LogWarning($"Incorrect resource type {currencyId}");
                return 0;
            }
        }
        
        public bool IsEnough(GameCurrencyID resourceType, float value)
        {
            return value <= GetValue(resourceType);
        }
        
        internal void Cleanup()
        {
            _currencyData.Clear();
        }
    }
}