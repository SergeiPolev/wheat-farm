using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// Radial (pie) menu overlay. Built programmatically; driven by RadialToolPresenter.
    /// Items are laid out on a circle; IndexFromScreenPoint maps the cursor angle to a slice.
    /// </summary>
    public class RadialMenuView : MonoBehaviour
    {
        private const float Radius = 220f;
        private const float Deadzone = 55f;

        private static readonly Color Dim = new(0f, 0f, 0f, 0.45f);
        private static readonly Color CellBg = new(0.15f, 0.15f, 0.15f, 0.92f);
        private static readonly Color CellHi = new(0.30f, 0.62f, 0.32f, 1f);

        private GameObject _panel;
        private RectTransform _center;
        private readonly List<Image> _cellBgs = new();
        private int _count;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public void Build(Transform canvasRoot)
        {
            _panel = new GameObject("RadialMenu");
            _panel.transform.SetParent(canvasRoot, false);
            var prect = _panel.AddComponent<RectTransform>();
            prect.anchorMin = Vector2.zero;
            prect.anchorMax = Vector2.one;
            prect.offsetMin = Vector2.zero;
            prect.offsetMax = Vector2.zero;
            var dim = _panel.AddComponent<Image>();
            dim.color = Dim;
            dim.raycastTarget = false;

            var centerGo = new GameObject("Center");
            centerGo.transform.SetParent(_panel.transform, false);
            _center = centerGo.AddComponent<RectTransform>();
            _center.anchorMin = new Vector2(0.5f, 0.5f);
            _center.anchorMax = new Vector2(0.5f, 0.5f);
            _center.anchoredPosition = Vector2.zero;
            _center.sizeDelta = Vector2.zero;

            // Center hint
            var hint = new GameObject("Hint");
            hint.transform.SetParent(_center, false);
            var hr = hint.AddComponent<RectTransform>();
            hr.sizeDelta = new Vector2(140, 40);
            hr.anchoredPosition = Vector2.zero;
            var htmp = hint.AddComponent<TextMeshProUGUI>();
            htmp.text = "Tools";
            htmp.fontSize = 16;
            htmp.alignment = TextAlignmentOptions.Center;
            htmp.color = new Color(1f, 1f, 1f, 0.55f);

            _panel.SetActive(false);
        }

        public void SetItems(string[] names)
        {
            // Clear existing cells (keep the Hint child at index 0)
            for (int i = _center.childCount - 1; i >= 0; i--)
            {
                var child = _center.GetChild(i);
                if (child.name == "Hint") continue;
                Destroy(child.gameObject);
            }
            _cellBgs.Clear();

            _count = names != null ? names.Length : 0;
            for (int i = 0; i < _count; i++)
            {
                float a = (90f - i * (360f / _count)) * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;

                var cell = new GameObject("Cell_" + names[i]);
                cell.transform.SetParent(_center, false);
                var r = cell.AddComponent<RectTransform>();
                r.sizeDelta = new Vector2(116, 56);
                r.anchoredPosition = pos;
                var bg = cell.AddComponent<Image>();
                bg.color = CellBg;
                _cellBgs.Add(bg);

                var label = new GameObject("Label");
                label.transform.SetParent(cell.transform, false);
                var lr = label.AddComponent<RectTransform>();
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero;
                lr.offsetMax = Vector2.zero;
                var tmp = label.AddComponent<TextMeshProUGUI>();
                tmp.text = names[i];
                tmp.fontSize = 15;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }
        }

        public void Show() { if (_panel != null) _panel.SetActive(true); }
        public void Hide() { if (_panel != null) _panel.SetActive(false); }

        public void SetHighlight(int idx)
        {
            for (int i = 0; i < _cellBgs.Count; i++)
                _cellBgs[i].color = (i == idx) ? CellHi : CellBg;
        }

        /// <summary>Map a screen-space point to the nearest slice index, or -1 inside the deadzone.</summary>
        public int IndexFromScreenPoint(Vector2 screenPos)
        {
            if (_count <= 0) return -1;
            Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 d = screenPos - c;
            if (d.magnitude < Deadzone) return -1;

            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            int best = -1;
            float bestDelta = 999f;
            for (int i = 0; i < _count; i++)
            {
                float a = 90f - i * (360f / _count);
                float delta = Mathf.Abs(Mathf.DeltaAngle(ang, a));
                if (delta < bestDelta) { bestDelta = delta; best = i; }
            }
            return best;
        }
    }
}
