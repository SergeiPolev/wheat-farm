using System;
using ObservableCollections;
using R3;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Inventory;

namespace WheatFarm.UI
{
    /// <summary>
    /// Inventory presenter — subscribes to ObservableList and drives InventoryView.
    /// </summary>
    public class InventoryPresenter : IInitializable, IDisposable
    {
        private readonly InventoryView _view;
        private readonly IInventoryService _inventory;
        private readonly CompositeDisposable _disposables = new();

        public InventoryPresenter(InventoryView view, IInventoryService inventory)
        {
            _view = view;
            _inventory = inventory;
        }

        public void Initialize()
        {
            // Subscribe to collection changes via typed event handler
            _inventory.Items.CollectionChanged += OnItemsChanged;

            _inventory.Capacity
                .Subscribe(_ => RefreshCapacity())
                .AddTo(_disposables);

            // Initial state
            RefreshAll();
        }

        public void Dispose()
        {
            _inventory.Items.CollectionChanged -= OnItemsChanged;
            _disposables.Dispose();
        }

        private void OnItemsChanged(in NotifyCollectionChangedEventArgs<InventoryItem> e)
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            var items = _inventory.Items;
            var names = new string[items.Count];
            var amounts = new int[items.Count];

            for (int i = 0; i < items.Count; i++)
            {
                names[i] = FormatItemName(items[i]);
                amounts[i] = items[i].Amount;
            }

            _view.SetSlots(names, amounts);
            RefreshCapacity();
        }

        private void RefreshCapacity()
        {
            _view.UpdateCapacity(_inventory.UsedSlots, _inventory.Capacity.CurrentValue);
        }

        private static string FormatItemName(InventoryItem item)
        {
            return item.Type switch
            {
                ItemType.Seed => $"{item.ItemId.Replace("seed_", "")} seed",
                ItemType.Dye => $"{item.ItemId.Replace("dye_", "")} dye",
                ItemType.Fertilizer => "Fertilizer",
                ItemType.Harvest => item.ItemId,
                ItemType.Product => item.ItemId,
                _ => item.ItemId
            };
        }
    }
}
