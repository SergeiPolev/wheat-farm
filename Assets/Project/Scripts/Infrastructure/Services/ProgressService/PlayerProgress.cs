using System;

namespace Data
{
    [Serializable]
    public class PlayerProgress
    {
        public WalletData WalletData = new WalletData();

        public PlayerProgress()
        {
            WalletData = new WalletData();
        }
    }
}
