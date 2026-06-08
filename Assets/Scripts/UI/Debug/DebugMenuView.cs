using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// Debug/cheat menu (built programmatically). A list of labelled toggles.
    /// Driven by DebugMenuPresenter.
    /// </summary>
    public class DebugMenuView : MonoBehaviour
    {
        private static readonly Color PanelBg = new(0.08f, 0.08f, 0.08f, 0.92f);

        private GameObject _panel;
        private readonly Dictionary<string, Toggle> _toggles = new();

        public event Action<string, bool> OnToggleChanged;
        public bool IsOpen => _panel != null && _panel.activeSelf;

        public void Build(Transform canvasRoot, (string key, string label)[] items)
        {
            _panel = new GameObject("DebugMenu");
            _panel.transform.SetParent(canvasRoot, false);
            var rect = _panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -80);
            rect.sizeDelta = new Vector2(240, 38 + items.Length * 32);
            _panel.AddComponent<Image>().color = PanelBg;

            var title = CreateLabel(_panel.transform, "Title", "DEBUG  (F1)", 16,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -6), new Vector2(-10, -30),
                TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;

            float y = -36f;
            foreach (var (key, label) in items)
            {
                var row = new GameObject("Row_" + key);
                row.transform.SetParent(_panel.transform, false);
                var rr = row.AddComponent<RectTransform>();
                rr.anchorMin = new Vector2(0, 1);
                rr.anchorMax = new Vector2(1, 1);
                rr.pivot = new Vector2(0, 1);
                rr.offsetMin = new Vector2(10, 0);
                rr.offsetMax = new Vector2(-10, 0);
                rr.anchoredPosition = new Vector2(0, y);
                rr.sizeDelta = new Vector2(0, 28);

                var tg = new GameObject("Toggle");
                tg.transform.SetParent(row.transform, false);
                var tgr = tg.AddComponent<RectTransform>();
                tgr.anchorMin = new Vector2(0, 0.5f);
                tgr.anchorMax = new Vector2(0, 0.5f);
                tgr.pivot = new Vector2(0, 0.5f);
                tgr.sizeDelta = new Vector2(20, 20);
                tgr.anchoredPosition = new Vector2(0, 0);
                var bgImg = tg.AddComponent<Image>();
                bgImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);

                var check = new GameObject("Check");
                check.transform.SetParent(tg.transform, false);
                var ckr = check.AddComponent<RectTransform>();
                ckr.anchorMin = new Vector2(0.15f, 0.15f);
                ckr.anchorMax = new Vector2(0.85f, 0.85f);
                ckr.offsetMin = Vector2.zero;
                ckr.offsetMax = Vector2.zero;
                var ckImg = check.AddComponent<Image>();
                ckImg.color = new Color(0.3f, 0.8f, 0.3f, 1f);

                var toggle = tg.AddComponent<Toggle>();
                toggle.targetGraphic = bgImg;
                toggle.graphic = ckImg;
                toggle.isOn = false;
                string k = key;
                toggle.onValueChanged.AddListener(v => OnToggleChanged?.Invoke(k, v));
                _toggles[key] = toggle;

                CreateLabel(row.transform, "Label", label, 14,
                    new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, 0), new Vector2(0, 0),
                    TextAlignmentOptions.Left);

                y -= 32f;
            }

            _panel.SetActive(false);
        }

        public void SetToggle(string key, bool value)
        {
            if (_toggles.TryGetValue(key, out var t))
                t.SetIsOnWithoutNotify(value);
        }

        public void Show() { if (_panel != null) _panel.SetActive(true); }
        public void Hide() { if (_panel != null) _panel.SetActive(false); }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, TextAlignmentOptions align)
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
            tmp.alignment = align;
            tmp.color = Color.white;
            return tmp;
        }
    }
}
