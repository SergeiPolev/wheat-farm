using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;

namespace WheatFarm.UI
{
    /// <summary>
    /// Building panel view — shows when player clicks an interactable building.
    /// Displays recipes, active production slots, auto-repeat toggle, and upgrade button.
    /// Pure visual: all actions forwarded to presenter via events.
    /// </summary>
    public class BuildingPanelView : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("Recipes")]
        [SerializeField] private Transform _recipeContainer;

        [Header("Active Slots")]
        [SerializeField] private Transform _slotContainer;

        [Header("Footer")]
        [SerializeField] private Toggle _autoRepeatToggle;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private TextMeshProUGUI _upgradeCostText;
        [SerializeField] private Button _closeButton;

        /// <summary>Raised when a "Start" button is clicked on a recipe row. Arg = recipe index.</summary>
        public event Action<int> OnStartRecipeClicked;

        /// <summary>Raised when a "Stop" button is clicked on an active slot. Arg = slot index.</summary>
        public event Action<int> OnStopSlotClicked;

        /// <summary>Raised when the upgrade button is clicked.</summary>
        public event Action OnUpgradeClicked;

        /// <summary>Raised when the close button is clicked.</summary>
        public event Action OnCloseClicked;

        /// <summary>Raised when the auto-repeat toggle changes. Arg = new state.</summary>
        public event Action<bool> OnAutoRepeatToggled;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());

            if (_upgradeButton != null)
                _upgradeButton.onClick.AddListener(() => OnUpgradeClicked?.Invoke());

            if (_autoRepeatToggle != null)
                _autoRepeatToggle.onValueChanged.AddListener(val => OnAutoRepeatToggled?.Invoke(val));
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

        public void SetTitle(string name, int level)
        {
            if (_titleText != null)
                _titleText.text = $"{name} (Lv.{level})";
        }

        /// <summary>
        /// Rebuild the recipe list. Each row shows "3x Wheat → 1 Flour" with a Start button.
        /// canStart callback determines if the Start button is interactable for each recipe index.
        /// </summary>
        public void SetRecipes(RecipeData[] recipes, Func<int, bool> canStart)
        {
            if (_recipeContainer == null) return;

            // Clear existing rows
            for (int i = _recipeContainer.childCount - 1; i >= 0; i--)
                Destroy(_recipeContainer.GetChild(i).gameObject);

            if (recipes == null) return;

            for (int i = 0; i < recipes.Length; i++)
            {
                var recipe = recipes[i];
                var row = CreateRecipeRow(recipe, i);
                row.transform.SetParent(_recipeContainer, false);

                var btn = row.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    btn.interactable = canStart != null && canStart(i);
                    int idx = i;
                    btn.onClick.AddListener(() => OnStartRecipeClicked?.Invoke(idx));
                }
            }
        }

        /// <summary>
        /// Rebuild the active slot list. Each row shows recipe name + progress bar + Stop button.
        /// </summary>
        public void SetActiveSlots(List<ProductionSlot> slots)
        {
            if (_slotContainer == null) return;

            // Clear existing rows
            for (int i = _slotContainer.childCount - 1; i >= 0; i--)
                Destroy(_slotContainer.GetChild(i).gameObject);

            if (slots == null || slots.Count == 0) return;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var row = CreateSlotRow(slot, i);
                row.transform.SetParent(_slotContainer, false);
            }
        }

        public void SetUpgradeInfo(int coinCost, int plankCost, bool canAfford, bool isMaxLevel)
        {
            if (_upgradeCostText != null)
            {
                _upgradeCostText.text = isMaxLevel
                    ? "MAX LEVEL"
                    : $"Upgrade: {coinCost}c + {plankCost} planks";
            }

            if (_upgradeButton != null)
                _upgradeButton.interactable = !isMaxLevel && canAfford;
        }

        public void SetAutoRepeat(bool enabled)
        {
            if (_autoRepeatToggle != null)
                _autoRepeatToggle.SetIsOnWithoutNotify(enabled);
        }

        // --- Row builders ---

        private static GameObject CreateRecipeRow(RecipeData recipe, int index)
        {
            var row = new GameObject($"Recipe_{index}");
            var rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 36);
            row.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.9f);
            row.AddComponent<LayoutElement>().preferredHeight = 36;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.padding = new RectOffset(6, 6, 2, 2);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Recipe text: "3x Wheat → 1 Flour"
            var textGo = new GameObject("RecipeText");
            textGo.transform.SetParent(row.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            var textLE = textGo.AddComponent<LayoutElement>();
            textLE.flexibleWidth = 1;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = FormatRecipe(recipe);
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = Color.white;

            // Start button
            var btnGo = new GameObject("StartBtn");
            btnGo.transform.SetParent(row.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            var btnLE = btnGo.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 52;
            btnLE.minWidth = 52;
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.3f, 0.6f, 0.3f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var btnLabel = new GameObject("Text");
            btnLabel.transform.SetParent(btnGo.transform, false);
            var btnLabelRect = btnLabel.AddComponent<RectTransform>();
            btnLabelRect.anchorMin = Vector2.zero;
            btnLabelRect.anchorMax = Vector2.one;
            btnLabelRect.offsetMin = Vector2.zero;
            btnLabelRect.offsetMax = Vector2.zero;
            var btnTmp = btnLabel.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "Start";
            btnTmp.fontSize = 12;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;

            return row;
        }

        private GameObject CreateSlotRow(ProductionSlot slot, int index)
        {
            var row = new GameObject($"Slot_{index}");
            var rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 32);
            row.AddComponent<Image>().color = new Color(0.15f, 0.2f, 0.15f, 0.9f);
            row.AddComponent<LayoutElement>().preferredHeight = 32;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.padding = new RectOffset(6, 6, 2, 2);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Slot label: recipe name
            var textGo = new GameObject("SlotText");
            textGo.transform.SetParent(row.transform, false);
            var textLE = textGo.AddComponent<LayoutElement>();
            textLE.preferredWidth = 80;
            textLE.flexibleWidth = 0;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = slot.Recipe != null ? slot.Recipe.DisplayName : "...";
            tmp.fontSize = 12;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = Color.white;

            // Progress bar (background + fill)
            var barBg = new GameObject("BarBg");
            barBg.transform.SetParent(row.transform, false);
            var barBgLE = barBg.AddComponent<LayoutElement>();
            barBgLE.flexibleWidth = 1;
            barBgLE.preferredHeight = 16;
            barBg.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 1f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(barBg.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(slot.Progress), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = new Color(0.3f, 0.7f, 0.3f, 1f);

            // Stop button
            var btnGo = new GameObject("StopBtn");
            btnGo.transform.SetParent(row.transform, false);
            var btnLE = btnGo.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 42;
            btnLE.minWidth = 42;
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.6f, 0.2f, 0.2f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            int idx = index;
            btn.onClick.AddListener(() => OnStopSlotClicked?.Invoke(idx));

            var btnLabel = new GameObject("Text");
            btnLabel.transform.SetParent(btnGo.transform, false);
            var btnLabelRect = btnLabel.AddComponent<RectTransform>();
            btnLabelRect.anchorMin = Vector2.zero;
            btnLabelRect.anchorMax = Vector2.one;
            btnLabelRect.offsetMin = Vector2.zero;
            btnLabelRect.offsetMax = Vector2.zero;
            var btnTmp = btnLabel.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "Stop";
            btnTmp.fontSize = 11;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;

            return row;
        }

        private static string FormatRecipe(RecipeData recipe)
        {
            if (recipe == null) return "???";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < recipe.Inputs.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append($"{recipe.Inputs[i].Amount}x {recipe.Inputs[i].ItemId}");
            }
            sb.Append(" \u2192 "); // → arrow
            sb.Append($"{recipe.Output.Amount}x {recipe.Output.ItemId}");
            return sb.ToString();
        }
    }
}
