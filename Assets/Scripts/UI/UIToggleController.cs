using UnityEngine;

namespace WheatFarm.UI
{
    /// <summary>
    /// Handles keyboard shortcuts for toggling UI panels.
    /// B = Shop, I = Inventory, C = Contracts (Tab = radial tool menu).
    /// </summary>
    public class UIToggleController : MonoBehaviour
    {
        private ShopView _shopView;
        private InventoryView _inventoryView;
        private ContractBoardView _contractView;
        [Header("Debug")]
        [Tooltip("Disable to turn off all debug panel hotkeys (B/I/C).")]
        [SerializeField] private bool _enabled = true;


        public void Init(ShopView shop, InventoryView inventory, ContractBoardView contracts = null)
        {
            _shopView = shop;
            _inventoryView = inventory;
            _contractView = contracts;
        }

        private void Update()
        {
            if (!_enabled) return;

            if (Input.GetKeyDown(KeyCode.B) && _shopView != null)
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
