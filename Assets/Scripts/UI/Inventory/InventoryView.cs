using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// Inventory view — toggle panel. Grid of item slots with icons and amounts.
    /// Pure visual: driven by InventoryPresenter.
    /// </summary>
    public class InventoryView : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;

        [Header("Grid")]
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private GameObject _slotPrefab;

        [Header("Footer")]
        [SerializeField] private TextMeshProUGUI _capacityText;
        [SerializeField] private Button _closeButton;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            if (_panel != null)
                _panel.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null)
                _panel.SetActive(false);
        }

        public void UpdateCapacity(int used, int max)
        {
            if (_capacityText != null)
                _capacityText.text = $"{used}/{max}";
        }

        /// <summary>
        /// Rebuild the slot grid from scratch. Called when items change.
        /// </summary>
        public void SetSlots(string[] itemNames, int[] amounts)
        {
            if (_slotContainer == null || _slotPrefab == null) return;

            // Clear existing
            for (int i = _slotContainer.childCount - 1; i >= 0; i--)
                Destroy(_slotContainer.GetChild(i).gameObject);

            // Create slots
            for (int i = 0; i < itemNames.Length; i++)
            {
                var go = Instantiate(_slotPrefab, _slotContainer);
                var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0) texts[0].text = itemNames[i];
                if (texts.Length > 1) texts[1].text = amounts[i].ToString();
            }
        }

        /// <summary>
        /// Update a single slot's amount without rebuilding the whole grid.
        /// </summary>
        public void UpdateSlot(int index, string itemName, int amount)
        {
            if (_slotContainer == null || index < 0 || index >= _slotContainer.childCount) return;

            var texts = _slotContainer.GetChild(index).GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = itemName;
            if (texts.Length > 1) texts[1].text = amount.ToString();
        }
    }
}
