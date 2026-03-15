using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// UI panel that appears when clicking on a placed building.
    /// Shows building info, recipes with input requirements, start buttons, and production progress.
    /// </summary>
    public class BuildingInteractionPanel : MonoBehaviour
    {
        private GameObject _panel;
        private TextMeshProUGUI _titleText;
        private Transform _recipeContainer;
        private Button _closeButton;

        private PlacedBuilding _currentBuilding;
        private readonly List<GameObject> _recipeEntries = new();

        public bool IsOpen => _panel != null && _panel.activeSelf;
        public PlacedBuilding CurrentBuilding => _currentBuilding;

        /// <summary>Raised when player clicks Start on a recipe. Args: building, recipe.</summary>
        public event Action<PlacedBuilding, RecipeData> OnStartRecipe;

        public void Show(PlacedBuilding building)
        {
            _currentBuilding = building;
            _titleText.text = building.Data.DisplayName;
            BuildRecipeList(building);
            _panel.SetActive(true);
        }

        public void Hide()
        {
            _panel.SetActive(false);
            _currentBuilding = null;
        }

        public void UpdateProgress(PlacedBuilding building, List<ProductionSlot> slots)
        {
            if (_currentBuilding != building || !IsOpen) return;
            // Update progress bars on recipe entries
            for (int i = 0; i < _recipeEntries.Count && i < (slots?.Count ?? 0); i++)
            {
                var slider = _recipeEntries[i].GetComponentInChildren<Slider>();
                if (slider != null)
                    slider.value = slots[i].Progress;
            }
        }

        private void BuildRecipeList(PlacedBuilding building)
        {
            // Clear old entries
            foreach (var entry in _recipeEntries)
                Destroy(entry);
            _recipeEntries.Clear();

            if (building.Data.Recipes == null) return;

            for (int i = 0; i < building.Data.Recipes.Length; i++)
            {
                var recipe = building.Data.Recipes[i];
                if (recipe == null) continue;

                var entry = CreateRecipeEntry(recipe, building);
                entry.transform.SetParent(_recipeContainer, false);
                _recipeEntries.Add(entry);
            }
        }

        private GameObject CreateRecipeEntry(RecipeData recipe, PlacedBuilding building)
        {
            var go = new GameObject($"Recipe_{recipe.RecipeId}");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 70);
            go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            go.AddComponent<LayoutElement>().preferredHeight = 70;

            // Recipe description: "3x wheat → 1x flour (15s)"
            string inputsStr = "";
            foreach (var input in recipe.Inputs)
                inputsStr += $"{input.Amount}x {input.ItemId} ";
            string desc = $"{inputsStr}→ {recipe.Output.Amount}x {recipe.Output.ItemId}\n({recipe.ProcessingTime}s)";

            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(go.transform, false);
            var descRect = descGo.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.35f);
            descRect.anchorMax = new Vector2(0.6f, 1);
            descRect.offsetMin = new Vector2(8, 0);
            descRect.offsetMax = Vector2.zero;
            var descTmp = descGo.AddComponent<TextMeshProUGUI>();
            descTmp.text = desc;
            descTmp.fontSize = 13;
            descTmp.alignment = TextAlignmentOptions.Left;
            descTmp.color = Color.white;

            // Start button
            var btnGo = new GameObject("StartBtn");
            btnGo.transform.SetParent(go.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.65f, 0.4f);
            btnRect.anchorMax = new Vector2(0.95f, 0.95f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.3f, 0.6f, 0.3f, 1);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var capturedRecipe = recipe;
            var capturedBuilding = building;
            btn.onClick.AddListener(() => OnStartRecipe?.Invoke(capturedBuilding, capturedRecipe));

            var btnLabel = new GameObject("Text");
            btnLabel.transform.SetParent(btnGo.transform, false);
            var btnLabelRect = btnLabel.AddComponent<RectTransform>();
            btnLabelRect.anchorMin = Vector2.zero;
            btnLabelRect.anchorMax = Vector2.one;
            btnLabelRect.offsetMin = Vector2.zero;
            btnLabelRect.offsetMax = Vector2.zero;
            var btnTmp = btnLabel.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "Start";
            btnTmp.fontSize = 14;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;

            // Progress slider (hidden until production starts)
            var sliderGo = new GameObject("Progress");
            sliderGo.transform.SetParent(go.transform, false);
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.02f, 0.05f);
            sliderRect.anchorMax = new Vector2(0.6f, 0.3f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            sliderGo.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = new Color(0.3f, 0.7f, 0.3f, 1);

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.interactable = false;
            slider.value = 0;

            return go;
        }

        /// <summary>
        /// Called by BuildingInteractionPresenter to build this panel at runtime.
        /// </summary>
        public static BuildingInteractionPanel Create(Transform canvasRoot)
        {
            var panelGo = new GameObject("BuildingInteractionPanel");
            panelGo.transform.SetParent(canvasRoot, false);
            var rect = panelGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400, 350);
            panelGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
            panelGo.SetActive(false);

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.85f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(10, 0);
            titleRect.offsetMax = new Vector2(-10, -5);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.fontSize = 24;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;

            // Recipe container with vertical layout
            var containerGo = new GameObject("RecipeContainer");
            containerGo.transform.SetParent(panelGo.transform, false);
            var containerRect = containerGo.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0.12f);
            containerRect.anchorMax = new Vector2(1, 0.83f);
            containerRect.offsetMin = new Vector2(10, 0);
            containerRect.offsetMax = new Vector2(-10, 0);
            var layout = containerGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(0, 0, 4, 4);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Close button
            var closeBtnGo = new GameObject("CloseBtn");
            closeBtnGo.transform.SetParent(panelGo.transform, false);
            var closeBtnRect = closeBtnGo.AddComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(0.75f, 0.02f);
            closeBtnRect.anchorMax = new Vector2(0.95f, 0.1f);
            closeBtnRect.offsetMin = Vector2.zero;
            closeBtnRect.offsetMax = Vector2.zero;
            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = new Color(0.6f, 0.2f, 0.2f, 1);
            var closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            var closeLabelGo = new GameObject("Text");
            closeLabelGo.transform.SetParent(closeBtnGo.transform, false);
            var closeLabelRect = closeLabelGo.AddComponent<RectTransform>();
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = Vector2.zero;
            closeLabelRect.offsetMax = Vector2.zero;
            var closeTmp = closeLabelGo.AddComponent<TextMeshProUGUI>();
            closeTmp.text = "X";
            closeTmp.fontSize = 16;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.color = Color.white;

            // Attach component and wire fields
            var panel = panelGo.AddComponent<BuildingInteractionPanel>();
            panel._panel = panelGo;
            panel._titleText = titleTmp;
            panel._recipeContainer = containerGo.transform;
            panel._closeButton = closeBtn;
            closeBtn.onClick.AddListener(() => panel.Hide());

            return panel;
        }
    }

    /// <summary>
    /// Connects FarmInteractionController.OnBuildingClicked to BuildingInteractionPanel.
    /// Starts production when player clicks "Start" on a recipe.
    /// </summary>
    public class BuildingClickHandler : VContainer.Unity.IInitializable, System.IDisposable
    {
        private readonly WheatFarm.Player.FarmInteractionController _interaction;
        private readonly BuildingInteractionPanel _panel;
        private readonly WheatFarm.Buildings.IProductionService _production;

        public BuildingClickHandler(
            WheatFarm.Player.FarmInteractionController interaction,
            BuildingInteractionPanel panel,
            WheatFarm.Buildings.IProductionService production)
        {
            _interaction = interaction;
            _panel = panel;
            _production = production;
        }

        public void Initialize()
        {
            _interaction.OnBuildingClicked += OnBuildingClicked;
            _panel.OnStartRecipe += OnStartRecipe;
        }

        private void OnBuildingClicked(GameObject go)
        {
            var marker = go.GetComponent<WheatFarm.Buildings.BuildingMarker>();
            if (marker == null || marker.Building == null) return;

            if (_panel.IsOpen && _panel.CurrentBuilding == marker.Building)
                _panel.Hide();
            else
                _panel.Show(marker.Building);
        }

        private void OnStartRecipe(WheatFarm.Buildings.PlacedBuilding building, WheatFarm.Core.Data.RecipeData recipe)
        {
            bool started = _production.TryStartProduction(building, recipe);
            Debug.Log(started
                ? "[Production] Started: " + recipe.DisplayName
                : "[Production] Cannot start " + recipe.DisplayName + " - missing inputs");
        }

        public void Dispose()
        {
            if (_interaction != null)
                _interaction.OnBuildingClicked -= OnBuildingClicked;
            if (_panel != null)
                _panel.OnStartRecipe -= OnStartRecipe;
        }
    }
}
