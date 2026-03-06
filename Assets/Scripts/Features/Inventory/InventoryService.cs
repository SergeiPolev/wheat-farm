using System.Linq;
using ObservableCollections;
using R3;
using WheatFarm.Core.Data;

namespace WheatFarm.Inventory
{
    public interface IInventoryService
    {
        ObservableList<InventoryItem> Items { get; }
        ReadOnlyReactiveProperty<int> Capacity { get; }
        int UsedSlots { get; }
        bool IsFull { get; }
        bool HasItem(string itemId, int amount = 1);
        bool TryConsume(string itemId, int amount = 1);
        bool TryAdd(InventoryItem item);
        void Clear();
    }

    public class InventoryService : IInventoryService
    {
        private readonly ReactiveProperty<int> _capacity = new(20);

        public ObservableList<InventoryItem> Items { get; } = new();
        public ReadOnlyReactiveProperty<int> Capacity => _capacity;
        public int UsedSlots => Items.Count;
        public bool IsFull => UsedSlots >= _capacity.Value;

        public bool HasItem(string itemId, int amount = 1)
        {
            int total = 0;
            foreach (var item in Items)
            {
                if (item.ItemId == itemId)
                    total += item.Amount;
            }
            return total >= amount;
        }

        public bool TryConsume(string itemId, int amount = 1)
        {
            if (!HasItem(itemId, amount)) return false;

            int remaining = amount;
            for (int i = Items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var item = Items[i];
                if (item.ItemId != itemId) continue;

                if (item.Amount <= remaining)
                {
                    remaining -= item.Amount;
                    Items.RemoveAt(i);
                }
                else
                {
                    item.Amount -= remaining;
                    Items[i] = item;
                    remaining = 0;
                }
            }
            return true;
        }

        public bool TryAdd(InventoryItem item)
        {
            // Try to stack with existing
            for (int i = 0; i < Items.Count; i++)
            {
                var existing = Items[i];
                if (existing.ItemId == item.ItemId && existing.Type == item.Type)
                {
                    existing.Amount += item.Amount;
                    Items[i] = existing;
                    return true;
                }
            }

            // New slot
            if (IsFull) return false;
            Items.Add(item);
            return true;
        }

        public void Clear() => Items.Clear();

        public void SetCapacity(int capacity) => _capacity.Value = capacity;
    }
}
