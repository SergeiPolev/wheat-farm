using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// Shop view — toggle panel. Shows catalog of seeds/dyes/fertilizer.
    /// Pure visual: buy events forwarded to presenter via callbacks.
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;

        [Header("Catalog")]
        [SerializeField] private Transform _itemContainer;
        [SerializeField] private GameObject _shopItemPrefab;

        [Header("Footer")]
        [SerializeField] private TextMeshProUGUI _coinsText;
        [SerializeField] private Button _closeButton;

        /// <summary>Raised when a buy button is clicked. Arg = catalog index.</summary>
        public event Action<int> OnBuyClicked;

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

        public void UpdateCoins(int amount)
        {
            if (_coinsText != null)
                _coinsText.text = amount.ToString();
        }

        /// <summary>
        /// Populate the shop catalog. Clears existing items and creates new ones.
        /// </summary>
        public void SetCatalog(string[] names, int[] prices)
        {
            if (_itemContainer == null || _shopItemPrefab == null) return;

            // Clear existing
            for (int i = _itemContainer.childCount - 1; i >= 0; i--)
                Destroy(_itemContainer.GetChild(i).gameObject);

            // Create entries
            for (int i = 0; i < names.Length; i++)
            {
                var go = Instantiate(_shopItemPrefab, _itemContainer);
                var nameText = go.GetComponentInChildren<TextMeshProUGUI>();
                if (nameText != null)
                    nameText.text = $"{names[i]}  ({prices[i]} coins)";

                int idx = i; // capture for lambda
                var btn = go.GetComponentInChildren<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => OnBuyClicked?.Invoke(idx));
            }
        }

        public void SetItemInteractable(int index, bool interactable)
        {
            if (_itemContainer == null || index < 0 || index >= _itemContainer.childCount) return;
            var btn = _itemContainer.GetChild(index).GetComponentInChildren<Button>();
            if (btn != null) btn.interactable = interactable;
        }
    }
}
