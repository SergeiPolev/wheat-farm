using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// Contract board view — toggle panel. Shows available contracts (accept)
    /// and active contracts (progress + complete) in a single list.
    /// Pure visual: events forwarded via callbacks.
    /// </summary>
    public class ContractBoardView : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;

        [Header("Available Contracts")]
        [SerializeField] private Transform _availableContainer;
        [SerializeField] private GameObject _availableEntryPrefab;

        [Header("Active Contracts")]
        [SerializeField] private Transform _contractContainer;
        [SerializeField] private GameObject _contractEntryPrefab;

        [Header("Footer")]
        [SerializeField] private Button _closeButton;

        /// <summary>Raised when an accept button is clicked. Arg = index in available list.</summary>
        public event Action<int> OnAcceptClicked;

        /// <summary>Raised when a complete button is clicked. Arg = index in active list.</summary>
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
        /// Rebuild the available contracts section.
        /// Each entry shows: description, reward, and an accept button.
        /// </summary>
        public void SetAvailableContracts(string[] descriptions, bool[] canAccept)
        {
            if (_availableContainer == null || _availableEntryPrefab == null) return;

            ClearContainer(_availableContainer);

            if (descriptions.Length == 0)
            {
                SpawnMessage(_availableContainer, _availableEntryPrefab, "No contracts available");
                return;
            }

            for (int i = 0; i < descriptions.Length; i++)
            {
                var go = Instantiate(_availableEntryPrefab, _availableContainer);
                go.SetActive(true);

                var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0) texts[0].text = descriptions[i];

                int idx = i;
                var buttons = go.GetComponentsInChildren<Button>();
                if (buttons.Length > 0)
                {
                    buttons[0].interactable = canAccept[i];
                    buttons[0].onClick.AddListener(() => OnAcceptClicked?.Invoke(idx));
                }
            }
        }

        /// <summary>
        /// Rebuild the active contracts section.
        /// Each entry shows: description, progress bar, and a complete button.
        /// </summary>
        public void SetContracts(string[] descriptions, float[] progress, bool[] canComplete)
        {
            if (_contractContainer == null || _contractEntryPrefab == null) return;

            ClearContainer(_contractContainer);

            if (descriptions.Length == 0)
            {
                SpawnMessage(_contractContainer, _contractEntryPrefab, "No active contracts");
                return;
            }

            for (int i = 0; i < descriptions.Length; i++)
            {
                var go = Instantiate(_contractEntryPrefab, _contractContainer);
                go.SetActive(true);

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
            if (_contractContainer != null && _contractEntryPrefab != null)
            {
                ClearContainer(_contractContainer);
                SpawnMessage(_contractContainer, _contractEntryPrefab, message);
            }

            if (_availableContainer != null && _availableEntryPrefab != null)
                ClearContainer(_availableContainer);
        }

        private static void ClearContainer(Transform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);
        }

        private static void SpawnMessage(Transform container, GameObject prefab, string message)
        {
            var go = Instantiate(prefab, container);
            go.SetActive(true);
            var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = message;

            // Hide buttons/sliders on message entries
            foreach (var btn in go.GetComponentsInChildren<Button>())
                btn.gameObject.SetActive(false);
            foreach (var slider in go.GetComponentsInChildren<Slider>())
                slider.gameObject.SetActive(false);
        }
    }
}
