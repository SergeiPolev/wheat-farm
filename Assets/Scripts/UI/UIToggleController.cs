using UnityEngine;

namespace WheatFarm.UI
{
    /// <summary>
    /// Handles keyboard shortcuts for toggling UI panels.
    /// Tab = Shop, I = Inventory, C = Contracts.
    /// </summary>
    public class UIToggleController : MonoBehaviour
    {
        private ShopView _shopView;
        private InventoryView _inventoryView;
        private ContractBoardView _contractView;

        public void Init(ShopView shop, InventoryView inventory, ContractBoardView contracts = null)
        {
            _shopView = shop;
            _inventoryView = inventory;
            _contractView = contracts;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab) && _shopView != null)
            {
                if (_shopView.IsOpen) _shopView.Hide();
                else _shopView.Show();
            }

            if (Input.GetKeyDown(KeyCode.I) && _inventoryView != null)
            {
                if (_inventoryView.IsOpen) _inventoryView.Hide();
                else _inventoryView.Show();
            }

            if (Input.GetKeyDown(KeyCode.C) && _contractView != null)
            {
                if (_contractView.IsOpen) _contractView.Hide();
                else _contractView.Show();
            }
        }
    }
}
