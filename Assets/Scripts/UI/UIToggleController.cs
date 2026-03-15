using UnityEngine;

namespace WheatFarm.UI
{
    /// <summary>
    /// Handles keyboard shortcuts for toggling UI panels.
    /// Tab = Shop, I = Inventory.
    /// Attach to any scene GameObject (auto-created by FarmScope).
    /// </summary>
    public class UIToggleController : MonoBehaviour
    {
        private ShopView _shopView;
        private InventoryView _inventoryView;

        public void Init(ShopView shop, InventoryView inventory)
        {
            _shopView = shop;
            _inventoryView = inventory;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab) && _shopView != null)
            {
                if (_shopView.IsOpen)
                    _shopView.Hide();
                else
                    _shopView.Show();
            }

            if (Input.GetKeyDown(KeyCode.I) && _inventoryView != null)
            {
                if (_inventoryView.IsOpen)
                    _inventoryView.Hide();
                else
                    _inventoryView.Show();
            }
        }
    }
}
