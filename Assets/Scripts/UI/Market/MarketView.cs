using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// Market "tablet" view — opens when the player clicks a Market building.
    /// Lists sellable harvest with prices and a Sell All action. Pure visual.
    /// </summary>
    public class MarketView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _itemContainer;
        [SerializeField] private GameObject _itemPrefab;
        [SerializeField] private TextMeshProUGUI _totalText;
        [SerializeField] private TextMeshProUGUI _coinsText;
        [SerializeField] private TextMeshProUGUI _emptyText;
        [SerializeField] private Button _sellAllButton;
        [SerializeField] private Button _closeButton;        [SerializeField] private Transform _buyContainer;
        [SerializeField] private GameObject _buyItemPrefab;


        public event Action OnSellAllClicked;
        public event Action OnCloseClicked;        public event Action<int> OnBuyClicked;


        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (_sellAllButton != null)
                _sellAllButton.onClick.AddListener(() => OnSellAllClicked?.Invoke());
            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
        }

        public void Show()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        public void SetItems(string[] names, int[] amounts, int[] lineTotals)
        {
            if (_itemContainer == null || _itemPrefab == null) return;

            for (int i = _itemContainer.childCount - 1; i >= 0; i--)
                Destroy(_itemContainer.GetChild(i).gameObject);

            int count = names != null ? names.Length : 0;
            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(_itemPrefab, _itemContainer);
                go.SetActive(true);
                var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0) texts[0].text = names[i];
                if (texts.Length > 1) texts[1].text = "x" + amounts[i];
                if (texts.Length > 2) texts[2].text = lineTotals[i] + "c";
            }

            if (_emptyText != null)
                _emptyText.gameObject.SetActive(count == 0);
        }

        public void UpdateTotal(int total, bool canSell)
        {
            if (_totalText != null) _totalText.text = "Total: " + total + "c";
            if (_sellAllButton != null) _sellAllButton.interactable = canSell;
        }

        public void UpdateCoins(int coins)
        {
            if (_coinsText != null) _coinsText.text = coins + "c";
        }

        public void SetBuyItems(string[] names, int[] prices, bool[] affordable)
        {
            if (_buyContainer == null || _buyItemPrefab == null) return;

            for (int i = _buyContainer.childCount - 1; i >= 0; i--)
                Destroy(_buyContainer.GetChild(i).gameObject);

            int count = names != null ? names.Length : 0;
            for (int i = 0; i < count; i++)
            {
                int index = i; // capture for closure
                var go = Instantiate(_buyItemPrefab, _buyContainer);
                go.SetActive(true);

                var label = go.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = names[i] + "  (" + prices[i] + "c)";

                var btn = go.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    btn.interactable = affordable == null || affordable[i];
                    btn.onClick.AddListener(() => OnBuyClicked?.Invoke(index));
                }
            }
        }

    }
}
