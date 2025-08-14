using System;

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
}