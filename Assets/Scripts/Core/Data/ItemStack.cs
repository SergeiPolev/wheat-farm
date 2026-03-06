namespace WheatFarm.Core.Data
{
    [System.Serializable]
    public struct ItemStack
    {
        public string ItemId;
        public int Amount;

        public ItemStack(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }
    }
}
