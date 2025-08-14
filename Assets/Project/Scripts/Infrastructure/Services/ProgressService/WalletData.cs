using System;
using System.Collections.Generic;
using Services;

namespace Data
{
    [Serializable]
    public class WalletData
    {
        public List<CurrencyData> CurrencyData;
        public WalletData()
        {
            CurrencyData = new List<CurrencyData>
            {
                new(CurrencyId.Coins, 0f),
                new(CurrencyId.Crystal, 0f)
            };
        }
    }
}