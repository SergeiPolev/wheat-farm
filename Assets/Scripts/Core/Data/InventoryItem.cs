namespace WheatFarm.Core.Data
{
    public enum ItemType
    {
        Seed,
        Dye,
        Fertilizer,
        Harvest,
        Product,
        Plank,
        Sapling
    }

    [System.Serializable]
    public struct InventoryItem
    {
        public string ItemId;
        public ItemType Type;
        public int Amount;

        public InventoryItem(string itemId, ItemType type, int amount)
        {
            ItemId = itemId;
            Type = type;
            Amount = amount;
        }
    }
}
