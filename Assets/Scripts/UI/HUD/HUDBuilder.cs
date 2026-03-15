using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// Programmatically creates the full HUD Canvas with all UI elements.
    /// Attach to any scene GameObject. Creates Canvas + EventSystem + HUDView
    /// on Awake, then destroys itself.
    /// </summary>
    public class HUDBuilder : MonoBehaviour
    {
        [Header("Output — set by builder, read by FarmScope")]
        public HUDView BuiltHUDView;

        private static readonly string[] ToolNames = { "Plant", "Water", "Sickle", "Dye", "Fert", "Uproot" };
        private static readonly Color ToolBgColor = new(0.15f, 0.15f, 0.15f, 0.7f);
        private static readonly Color PanelBgColor = new(0.1f, 0.1f, 0.1f, 0.75f);

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            // EventSystem (if not already present)
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            // Canvas
            var canvasGo = new GameObject("HUDCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // HUDView component on canvas root
            var hudView = canvasGo.AddComponent<HUDView>();

            // === Coins Panel (top-left) ===
            var coinsPanel = CreatePanel(canvasGo.transform, "CoinsPanel",
                TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -20), new Vector2(220, 50));

            var coinsText = CreateTMPText(coinsPanel.transform, "CoinsText",
                "100", 28, TextAlignmentOptions.Left,
                new Vector2(10, 0), new Vector2(-10, 0));

            // Coin icon label
            var coinLabel = CreateTMPText(coinsPanel.transform, "CoinLabel",
                "<color=#FFD700>$</color>", 28, TextAlignmentOptions.Right,
                new Vector2(10, 0), new Vector2(-10, 0));

            // === Tool Bar (bottom-center) ===
            var toolBar = CreatePanel(canvasGo.transform, "ToolBar",
                TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 15), new Vector2(420, 65));

            var toolLayout = toolBar.AddComponent<HorizontalLayoutGroup>();
            toolLayout.spacing = 6;
            toolLayout.padding = new RectOffset(8, 8, 6, 6);
            toolLayout.childAlignment = TextAnchor.MiddleCenter;
            toolLayout.childForceExpandWidth = false;
            toolLayout.childForceExpandHeight = false;

            var toolIcons = new Image[ToolNames.Length];
            for (int i = 0; i < ToolNames.Length; i++)
            {
                var toolGo = new GameObject($"Tool_{ToolNames[i]}");
                toolGo.transform.SetParent(toolBar.transform, false);

                var toolImg = toolGo.AddComponent<Image>();
                toolImg.color = ToolBgColor;

                var toolLE = toolGo.AddComponent<LayoutElement>();
                toolLE.preferredWidth = 60;
                toolLE.preferredHeight = 50;

                var toolLabel = CreateTMPText(toolGo.transform, "Label",
                    $"{i + 1}\n{ToolNames[i]}", 14, TextAlignmentOptions.Center,
                    Vector2.zero, Vector2.zero, true);

                toolIcons[i] = toolImg;
            }

            // === Time Panel (top-right) ===
            var timePanel = CreatePanel(canvasGo.transform, "TimePanel",
                TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-20, -20), new Vector2(180, 50));

            var timeText = CreateTMPText(timePanel.transform, "TimeText",
                "Day", 22, TextAlignmentOptions.Center,
                new Vector2(5, 0), new Vector2(-5, 0));

            // Time fill bar below time panel
            var fillBg = new GameObject("TimeFillBg");
            fillBg.transform.SetParent(timePanel.transform, false);
            var fillBgRect = fillBg.AddComponent<RectTransform>();
            fillBgRect.anchorMin = new Vector2(0, 0);
            fillBgRect.anchorMax = new Vector2(1, 0);
            fillBgRect.pivot = new Vector2(0, 1);
            fillBgRect.anchoredPosition = new Vector2(0, 2);
            fillBgRect.sizeDelta = new Vector2(0, 6);
            var fillBgImg = fillBg.AddComponent<Image>();
            fillBgImg.color = new Color(0, 0, 0, 0.4f);

            var fillGo = new GameObject("TimeFill");
            fillGo.transform.SetParent(fillBg.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0.25f;

            // === Wire HUDView serialized fields via reflection ===
            SetPrivateField(hudView, "_coinsText", coinsText);
            SetPrivateField(hudView, "_toolIcons", toolIcons);
            SetPrivateField(hudView, "_timeText", timeText);
            SetPrivateField(hudView, "_timeFill", fillImg);

            BuiltHUDView = hudView;
        }

        private static GameObject CreatePanel(Transform parent, string name,
            TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;

            var img = go.AddComponent<Image>();
            img.color = PanelBgColor;

            return go;
        }

        private static TextMeshProUGUI CreateTMPText(Transform parent, string name,
            string text, float fontSize, TextAlignmentOptions alignment,
            Vector2 offsetMin, Vector2 offsetMax, bool stretch = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();

            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.enableAutoSizing = false;

            return tmp;
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
