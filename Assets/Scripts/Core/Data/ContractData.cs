namespace WheatFarm.Core.Data
{
    /// <summary>
    /// Definition of an optional contract. Contracts provide bonus rewards
    /// for fulfilling specific harvest/production requirements.
    /// </summary>
    [System.Serializable]
    public class ContractData
    {
        public string ContractId;
        public string Description;
        public ItemStack[] Required;
        public int CoinReward;
        /// <summary>Nullable — reward: unlock a new plant type.</summary>
        public string UnlockPlantId;
        /// <summary>Nullable — reward: unlock a dye color (free grant, no coin cost).</summary>
        public string UnlockDyeId;
        /// <summary>Nullable — reward: unlock a building (free grant).</summary>
        public string UnlockBuildingId;
        /// <summary>Reward multiplier over base sell price (1.5-3x).</summary>
        public float RewardMultiplier = 1.5f;
    }
}
