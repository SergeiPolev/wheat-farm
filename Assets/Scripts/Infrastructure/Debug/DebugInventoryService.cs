using ObservableCollections;
using R3;
using WheatFarm.Core;
using WheatFarm.Core.Data;
using WheatFarm.Inventory;

namespace WheatFarm.Infrastructure.Cheats
{
    /// <summary>
    /// Debug decorator over IInventoryService. When seeds/resources are free, HasItem is
    /// always true and TryConsume never depletes. Forwards everything else. The Inventory
    /// assembly has NO debug dependency.
    /// </summary>
    public class DebugInventoryService : IInventoryService
    {
        private readonly IInventoryService _inner;
        private readonly IDebugFlags _debug;

        public DebugInventoryService(InventoryService inner, IDebugFlags debug)
        {
            _inner = inner;
            _debug = debug;
        }

        public ObservableList<InventoryItem> Items => _inner.Items;
        public ReadOnlyReactiveProperty<int> Capacity => _inner.Capacity;
        public int UsedSlots => _inner.UsedSlots;
        public bool IsFull => _inner.IsFull;

        public int GetAmount(string itemId) => _inner.GetAmount(itemId);
        public bool TryAdd(InventoryItem item) => _inner.TryAdd(item);
        public void Clear() => _inner.Clear();
        public void Dispose() => _inner.Dispose();

        public bool HasItem(string itemId, int amount = 1)
        {
            if (IsFree(itemId)) return true;
            return _inner.HasItem(itemId, amount);
        }

        public bool TryConsume(string itemId, int amount = 1)
        {
            if (IsFree(itemId)) return true; // never deplete
            return _inner.TryConsume(itemId, amount);
        }

        private bool IsFree(string itemId) =>
            itemId != null && itemId.StartsWith("seed_") ? _debug.SeedsAreFree : _debug.ResourcesAreFree;
    }
}
