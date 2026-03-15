using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// Programmatic category tab bar + item panel at bottom of screen.
    /// View only — CatalogPresenter drives data and handles clicks.
    /// </summary>
    public class CatalogTabBar : MonoBehaviour
    {
        // Tabs
        private readonly List<Button> _tabButtons = new();
        private readonly List<Image> _tabImages = new();
        private int _activeTabIndex = -1;

        // Item panel
        private GameObject _itemPanel;
        private Transform _itemContainer;
        private GameObject _itemPrefab;
        private readonly List<GameObject> _itemInstances = new();

        private static readonly Color TabBg = new(0.15f, 0.15f, 0.15f, 0.8f);
        private static readonly Color TabActive = new(0.3f, 0.5f, 0.3f, 0.9f);
        private static readonly Color TabInactive = new(0.18f, 0.18f, 0.18f, 0.8f);
        private static readonly Color PanelBg = new(0.1f, 0.1f, 0.1f, 0.85f);
        private static readonly Color ItemBg = new(0.22f, 0.22f, 0.22f, 0.9f);
        private static readonly Color ItemHover = new(0.3f, 0.45f, 0.3f, 0.9f);

        /// <summary>Fired when a tab is clicked. Arg = tab index.</summary>
        public event Action<int> OnTabClicked;

        /// <summary>Fired when an item in the panel is clicked. Arg = item index within current tab.</summary>
        public event Action<int> OnItemClicked;

        /// <summary>Build the entire UI hierarchy. Called once after AddComponent.</summary>
        public void Build(Transform canvasRoot, string[] tabNames)
        {
            // === Tab bar (bottom of screen, horizontal) ===
            var tabBar = new GameObject("CatalogTabBar");
            tabBar.transform.SetParent(canvasRoot, false);
            var tabBarRect = tabBar.AddComponent<RectTransform>();
            tabBarRect.anchorMin = new Vector2(0.1f, 0);
            tabBarRect.anchorMax = new Vector2(0.9f, 0);
            tabBarRect.pivot = new Vector2(0.5f, 0);
            tabBarRect.anchoredPosition = new Vector2(0, 0);
            tabBarRect.sizeDelta = new Vector2(0, 44);

            var tabBarBg = tabBar.AddComponent<Image>();
            tabBarBg.color = TabBg;

            var tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 2;
            tabLayout.padding = new RectOffset(4, 4, 2, 2);
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;
            tabLayout.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < tabNames.Length; i++)
            {
                var tabGo = new GameObject($"Tab_{tabNames[i]}");
                tabGo.transform.SetParent(tabBar.transform, false);

                var tabImg = tabGo.AddComponent<Image>();
                tabImg.color = TabInactive;
                _tabImages.Add(tabImg);

                var tabBtn = tabGo.AddComponent<Button>();
                tabBtn.targetGraphic = tabImg;
                int idx = i;
                tabBtn.onClick.AddListener(() => OnTabClicked?.Invoke(idx));
                _tabButtons.Add(tabBtn);

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(tabGo.transform, false);
                var labelRect = labelGo.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = tabNames[i];
                label.fontSize = 16;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
            }

            // === Item panel (above tab bar) ===
            _itemPanel = new GameObject("CatalogItemPanel");
            _itemPanel.transform.SetParent(canvasRoot, false);
            var panelRect = _itemPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0);
            panelRect.anchorMax = new Vector2(0.9f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 46);
            panelRect.sizeDelta = new Vector2(0, 120);

            var panelBg = _itemPanel.AddComponent<Image>();
            panelBg.color = PanelBg;

            // Scroll area inside panel
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(_itemPanel.transform, false);
            var scrollRect = scrollArea.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(6, 4);
            scrollRect.offsetMax = new Vector2(-6, -4);

            var itemLayout = scrollArea.AddComponent<HorizontalLayoutGroup>();
            itemLayout.spacing = 6;
            itemLayout.padding = new RectOffset(2, 2, 2, 2);
            itemLayout.childForceExpandWidth = false;
            itemLayout.childForceExpandHeight = true;
            itemLayout.childAlignment = TextAnchor.MiddleLeft;

            _itemContainer = scrollArea.transform;

            // Item prefab (detached template)
            _itemPrefab = CreateItemPrefab();
            _itemPrefab.SetActive(false);

            // Start hidden
            _itemPanel.SetActive(false);
        }

        public void SetActiveTab(int index)
        {
            _activeTabIndex = index;
            for (int i = 0; i < _tabImages.Count; i++)
                _tabImages[i].color = (i == index) ? TabActive : TabInactive;
            _itemPanel.SetActive(index >= 0);
        }

        /// <summary>Populate item panel with items. Each entry: (name, cost, locked).</summary>
        public void SetItems(List<(string name, int cost, bool locked)> items)
        {
            // Clear old items
            foreach (var inst in _itemInstances)
                Destroy(inst);
            _itemInstances.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                var go = Instantiate(_itemPrefab, _itemContainer);
                go.SetActive(true);

                var nameText = go.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                var costText = go.transform.Find("Cost")?.GetComponent<TextMeshProUGUI>();
                var btn = go.GetComponent<Button>();

                if (nameText != null) nameText.text = items[i].name;
                if (costText != null)
                    costText.text = items[i].cost > 0 ? $"{items[i].cost}c" : "";

                if (items[i].locked)
                {
                    var img = go.GetComponent<Image>();
                    if (img != null) img.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
                    if (btn != null) btn.interactable = false;
                }

                int idx = i;
                if (btn != null)
                    btn.onClick.AddListener(() => OnItemClicked?.Invoke(idx));

                _itemInstances.Add(go);
            }
        }

        public void Hide()
        {
            _itemPanel.SetActive(false);
            _activeTabIndex = -1;
            for (int i = 0; i < _tabImages.Count; i++)
                _tabImages[i].color = TabInactive;
        }

        private GameObject CreateItemPrefab()
        {
            var go = new GameObject("CatalogItem");

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(90, 100);

            var img = go.AddComponent<Image>();
            img.color = ItemBg;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 90;
            le.preferredHeight = 100;

            go.AddComponent<Button>().targetGraphic = img;

            // Name label (top)
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(go.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.4f);
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(2, 0);
            nameRect.offsetMax = new Vector2(-2, -2);

            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.fontSize = 12;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            nameText.enableWordWrapping = true;

            // Cost label (bottom)
            var costGo = new GameObject("Cost");
            costGo.transform.SetParent(go.transform, false);
            var costRect = costGo.AddComponent<RectTransform>();
            costRect.anchorMin = Vector2.zero;
            costRect.anchorMax = new Vector2(1, 0.35f);
            costRect.offsetMin = new Vector2(2, 2);
            costRect.offsetMax = new Vector2(-2, 0);

            var costText = costGo.AddComponent<TextMeshProUGUI>();
            costText.fontSize = 13;
            costText.alignment = TextAlignmentOptions.Center;
            costText.color = new Color(1f, 0.85f, 0.3f);

            return go;
        }
    }
}
