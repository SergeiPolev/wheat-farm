using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// Programmatically creates Shop and Inventory panels at runtime.
    /// Called from FarmScope.Configure if views not assigned.
    /// </summary>
    public static class PanelBuilder
    {
        private static readonly Color PanelBg = new(0.08f, 0.08f, 0.08f, 0.9f);
        private static readonly Color ItemBg = new(0.2f, 0.2f, 0.2f, 0.8f);
        private static readonly Color ButtonColor = new(0.3f, 0.6f, 0.3f, 1f);
        private static readonly Color CloseColor = new(0.6f, 0.2f, 0.2f, 1f);

        public static ShopView BuildShopPanel(Transform canvasRoot)
        {
            // Main panel (center, hidden by default)
            var panel = CreatePanel(canvasRoot, "ShopPanel", 400, 500);
            panel.SetActive(false);

            var panelRect = panel.GetComponent<RectTransform>();

            // Title
            CreateLabel(panel.transform, "Title", "SHOP", 24, TextAnchor.UpperCenter,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, -10), new Vector2(-10, -40));

            // Item container with vertical layout
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(panel.transform, false);
            var scrollRect = scrollArea.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0.15f);
            scrollRect.anchorMax = new Vector2(1, 0.85f);
            scrollRect.offsetMin = new Vector2(10, 0);
            scrollRect.offsetMax = new Vector2(-10, 0);

            var layout = scrollArea.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(0, 0, 4, 4);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Item prefab template (will be used by ShopView.SetCatalog)
            var itemPrefab = CreateItemPrefab("ShopItemPrefab", ButtonColor);
            itemPrefab.SetActive(false);

            // Footer: coins + close button
            var footer = new GameObject("Footer");
            footer.transform.SetParent(panel.transform, false);
            var footerRect = footer.AddComponent<RectTransform>();
            footerRect.anchorMin = Vector2.zero;
            footerRect.anchorMax = new Vector2(1, 0.15f);
            footerRect.offsetMin = new Vector2(10, 5);
            footerRect.offsetMax = new Vector2(-10, 0);

            var coinsText = CreateTMP(footer.transform, "Coins", "100 coins", 18);
            var coinsRect = coinsText.GetComponent<RectTransform>();
            coinsRect.anchorMin = new Vector2(0, 0);
            coinsRect.anchorMax = new Vector2(0.6f, 1);
            coinsRect.offsetMin = Vector2.zero;
            coinsRect.offsetMax = Vector2.zero;

            var closeBtn = CreateButton(footer.transform, "Close", "X", CloseColor);
            var closeBtnRect = closeBtn.GetComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(0.75f, 0.1f);
            closeBtnRect.anchorMax = new Vector2(1f, 0.9f);
            closeBtnRect.offsetMin = Vector2.zero;
            closeBtnRect.offsetMax = Vector2.zero;

            // Attach ShopView and wire fields
            var view = panel.AddComponent<ShopView>();
            SetField(view, "_panel", panel);
            SetField(view, "_itemContainer", scrollArea.transform);
            SetField(view, "_shopItemPrefab", itemPrefab);
            SetField(view, "_coinsText", coinsText);
            SetField(view, "_closeButton", closeBtn);

            return view;
        }

        public static InventoryView BuildInventoryPanel(Transform canvasRoot)
        {
            var panel = CreatePanel(canvasRoot, "InventoryPanel", 350, 450);
            panel.SetActive(false);

            // Title
            CreateLabel(panel.transform, "Title", "INVENTORY", 24, TextAnchor.UpperCenter,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, -10), new Vector2(-10, -40));

            // Slot container
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(panel.transform, false);
            var scrollRect = scrollArea.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0.15f);
            scrollRect.anchorMax = new Vector2(1, 0.85f);
            scrollRect.offsetMin = new Vector2(10, 0);
            scrollRect.offsetMax = new Vector2(-10, 0);

            var layout = scrollArea.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(0, 0, 4, 4);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Slot prefab
            var slotPrefab = CreateSlotPrefab("InvSlotPrefab");
            slotPrefab.SetActive(false);

            // Footer: capacity + close
            var footer = new GameObject("Footer");
            footer.transform.SetParent(panel.transform, false);
            var footerRect = footer.AddComponent<RectTransform>();
            footerRect.anchorMin = Vector2.zero;
            footerRect.anchorMax = new Vector2(1, 0.15f);
            footerRect.offsetMin = new Vector2(10, 5);
            footerRect.offsetMax = new Vector2(-10, 0);

            var capText = CreateTMP(footer.transform, "Capacity", "0/20", 18);
            var capRect = capText.GetComponent<RectTransform>();
            capRect.anchorMin = new Vector2(0, 0);
            capRect.anchorMax = new Vector2(0.6f, 1);
            capRect.offsetMin = Vector2.zero;
            capRect.offsetMax = Vector2.zero;

            var closeBtn = CreateButton(footer.transform, "Close", "X", CloseColor);
            var closeBtnRect = closeBtn.GetComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(0.75f, 0.1f);
            closeBtnRect.anchorMax = new Vector2(1f, 0.9f);
            closeBtnRect.offsetMin = Vector2.zero;
            closeBtnRect.offsetMax = Vector2.zero;

            var view = panel.AddComponent<InventoryView>();
            SetField(view, "_panel", panel);
            SetField(view, "_slotContainer", scrollArea.transform);
            SetField(view, "_slotPrefab", slotPrefab);
            SetField(view, "_capacityText", capText);
            SetField(view, "_closeButton", closeBtn);

            return view;
        }

        public static ContractBoardView BuildContractPanel(Transform canvasRoot)
        {
            var panel = CreatePanel(canvasRoot, "ContractPanel", 450, 400);
            panel.SetActive(false);

            CreateLabel(panel.transform, "Title", "CONTRACTS", 24, TextAnchor.UpperCenter,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, -10), new Vector2(-10, -40));

            // Contract container
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(panel.transform, false);
            var scrollRect = scrollArea.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0.15f);
            scrollRect.anchorMax = new Vector2(1, 0.85f);
            scrollRect.offsetMin = new Vector2(10, 0);
            scrollRect.offsetMax = new Vector2(-10, 0);

            var layout = scrollArea.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(0, 0, 4, 4);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Contract entry prefab: description text + slider + complete button
            var entryPrefab = CreateContractEntryPrefab();
            entryPrefab.SetActive(false);

            // Footer: close
            var footer = new GameObject("Footer");
            footer.transform.SetParent(panel.transform, false);
            var footerRect = footer.AddComponent<RectTransform>();
            footerRect.anchorMin = Vector2.zero;
            footerRect.anchorMax = new Vector2(1, 0.15f);
            footerRect.offsetMin = new Vector2(10, 5);
            footerRect.offsetMax = new Vector2(-10, 0);

            var closeBtn = CreateButton(footer.transform, "Close", "X", CloseColor);
            var closeBtnRect = closeBtn.GetComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(0.75f, 0.1f);
            closeBtnRect.anchorMax = new Vector2(1f, 0.9f);
            closeBtnRect.offsetMin = Vector2.zero;
            closeBtnRect.offsetMax = Vector2.zero;

            var view = panel.AddComponent<ContractBoardView>();
            SetField(view, "_panel", panel);
            SetField(view, "_contractContainer", scrollArea.transform);
            SetField(view, "_contractEntryPrefab", entryPrefab);
            SetField(view, "_closeButton", closeBtn);

            return view;
        }

        /// <summary>Contract entry: description TMP, Slider for progress, Button for complete.</summary>
        private static GameObject CreateContractEntryPrefab()
        {
            var go = new GameObject("ContractEntry");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 60);
            go.AddComponent<Image>().color = ItemBg;
            go.AddComponent<LayoutElement>().preferredHeight = 60;

            // Description label (GetComponentsInChildren<TMP>[0])
            var desc = new GameObject("Desc");
            desc.transform.SetParent(go.transform, false);
            var descRect = desc.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.4f);
            descRect.anchorMax = new Vector2(0.95f, 1);
            descRect.offsetMin = new Vector2(8, 0);
            descRect.offsetMax = Vector2.zero;
            var descTmp = desc.AddComponent<TextMeshProUGUI>();
            descTmp.fontSize = 13;
            descTmp.alignment = TextAlignmentOptions.Left;
            descTmp.color = Color.white;

            // Progress slider (GetComponentsInChildren<Slider>[0])
            var sliderGo = new GameObject("Progress");
            sliderGo.transform.SetParent(go.transform, false);
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.02f, 0.1f);
            sliderRect.anchorMax = new Vector2(0.6f, 0.38f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            var sliderBg = sliderGo.AddComponent<Image>();
            sliderBg.color = new Color(0.15f, 0.15f, 0.15f, 1);

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
            slider.minValue = 0;
            slider.maxValue = 1;

            // Complete button (GetComponentsInChildren<Button>[0])
            var btnGo = new GameObject("CompleteBtn");
            btnGo.transform.SetParent(go.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.65f, 0.05f);
            btnRect.anchorMax = new Vector2(0.95f, 0.38f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = ButtonColor;
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
            btnTmp.text = "Done";
            btnTmp.fontSize = 12;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;

            return go;
        }

        // --- Helpers ---

        private static GameObject CreatePanel(Transform parent, string name, float width, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);

            var img = go.AddComponent<Image>();
            img.color = PanelBg;
            return go;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
            float fontSize, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = Color.white;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }

        /// <summary>Shop item prefab: label + buy button in a horizontal row.</summary>
        private static GameObject CreateItemPrefab(string name, Color btnColor)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 40);

            var bg = go.AddComponent<Image>();
            bg.color = ItemBg;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 40;

            // Name + price label (TextMeshProUGUI found by ShopView via GetComponentInChildren)
            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0.65f, 1);
            labelRect.offsetMin = new Vector2(8, 0);
            labelRect.offsetMax = Vector2.zero;
            var labelTmp = label.AddComponent<TextMeshProUGUI>();
            labelTmp.fontSize = 16;
            labelTmp.alignment = TextAlignmentOptions.Left;
            labelTmp.color = Color.white;

            // Buy button (Button found by ShopView via GetComponentInChildren)
            var btnGo = new GameObject("BuyBtn");
            btnGo.transform.SetParent(go.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.7f, 0.1f);
            btnRect.anchorMax = new Vector2(0.95f, 0.9f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = btnColor;
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
            btnTmp.text = "Buy";
            btnTmp.fontSize = 14;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;

            return go;
        }

        /// <summary>Inventory slot prefab: two TMP texts (name, amount).</summary>
        private static GameObject CreateSlotPrefab(string name)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 36);

            var bg = go.AddComponent<Image>();
            bg.color = ItemBg;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 36;

            // Item name (first TMP found by InventoryView)
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(go.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(0.7f, 1);
            nameRect.offsetMin = new Vector2(8, 0);
            nameRect.offsetMax = Vector2.zero;
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.fontSize = 16;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.color = Color.white;

            // Amount (second TMP found by InventoryView)
            var amtGo = new GameObject("Amount");
            amtGo.transform.SetParent(go.transform, false);
            var amtRect = amtGo.AddComponent<RectTransform>();
            amtRect.anchorMin = new Vector2(0.7f, 0);
            amtRect.anchorMax = new Vector2(1, 1);
            amtRect.offsetMin = Vector2.zero;
            amtRect.offsetMax = new Vector2(-8, 0);
            var amtTmp = amtGo.AddComponent<TextMeshProUGUI>();
            amtTmp.fontSize = 16;
            amtTmp.alignment = TextAlignmentOptions.Right;
            amtTmp.color = new Color(0.8f, 0.8f, 0.3f);

            return go;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
