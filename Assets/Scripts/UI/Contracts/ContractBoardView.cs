using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// Contract board view — toggle panel. Shows active contracts with progress.
    /// Pure visual: complete events forwarded via callbacks.
    /// </summary>
    public class ContractBoardView : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;

        [Header("Contracts")]
        [SerializeField] private Transform _contractContainer;
        [SerializeField] private GameObject _contractEntryPrefab;

        [Header("Footer")]
        [SerializeField] private Button _closeButton;

        /// <summary>Raised when a complete button is clicked. Arg = contract index.</summary>
        public event Action<int> OnCompleteClicked;

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

        /// <summary>
        /// Rebuild the contract list from active contracts.
        /// Each entry shows: description, progress, and a complete button.
        /// </summary>
        public void SetContracts(string[] descriptions, float[] progress, bool[] canComplete)
        {
            if (_contractContainer == null || _contractEntryPrefab == null) return;

            // Clear existing
            for (int i = _contractContainer.childCount - 1; i >= 0; i--)
                Destroy(_contractContainer.GetChild(i).gameObject);

            for (int i = 0; i < descriptions.Length; i++)
            {
                var go = Instantiate(_contractEntryPrefab, _contractContainer);

                // Description
                var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0) texts[0].text = descriptions[i];

                // Progress bar
                var sliders = go.GetComponentsInChildren<Slider>();
                if (sliders.Length > 0)
                {
                    sliders[0].value = progress[i];
                    sliders[0].interactable = false;
                }

                // Complete button
                int idx = i;
                var buttons = go.GetComponentsInChildren<Button>();
                if (buttons.Length > 0)
                {
                    buttons[0].interactable = canComplete[i];
                    buttons[0].onClick.AddListener(() => OnCompleteClicked?.Invoke(idx));
                }
            }
        }

        public void ShowEmpty(string message)
        {
            if (_contractContainer == null || _contractEntryPrefab == null) return;

            for (int i = _contractContainer.childCount - 1; i >= 0; i--)
                Destroy(_contractContainer.GetChild(i).gameObject);

            var go = Instantiate(_contractEntryPrefab, _contractContainer);
            var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = message;
        }
    }
}
