using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheatFarm.Core.Data;

namespace WheatFarm.UI
{
    /// <summary>
    /// HUD palette of dye swatches. Pure view: builds swatch buttons, renders per-swatch state,
    /// raises a click event. All economy/selection logic lives in the presenter.
    /// </summary>
    public interface IDyeColorPaletteView
    {
        event Action<int> SwatchClicked;
        void Build(IReadOnlyList<DyeData> dyes);
        void SetVisible(bool visible);
        void SetSwatchState(int index, bool unlocked, bool selected, int cost);
    }

    public class DyeColorPaletteView : MonoBehaviour, IDyeColorPaletteView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _container;

        private const float SwatchSize = 44f;

        private readonly List<Swatch> _swatches = new();

        public event Action<int> SwatchClicked;

        private class Swatch
        {
            public Color BaseColor;
            public Image Fill;
            public Image Outline;
            public TextMeshProUGUI Price;
        }

        public void SetVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
            else gameObject.SetActive(visible);
        }

        public void Build(IReadOnlyList<DyeData> dyes)
        {
            var stale = new List<GameObject>();
            foreach (Transform c in _container) stale.Add(c.gameObject);
            foreach (var go in stale) Destroy(go);
            _swatches.Clear();

            if (dyes == null) return;

            for (int i = 0; i < dyes.Count; i++)
            {
                int idx = i;
                var dye = dyes[i];

                var go = new GameObject($"Swatch_{dye.DyeId}", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_container, false);
                ((RectTransform)go.transform).sizeDelta = new Vector2(SwatchSize, SwatchSize);
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = SwatchSize;
                le.preferredHeight = SwatchSize;

                var fill = go.GetComponent<Image>();
                fill.color = dye.Color;

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = fill;
                btn.onClick.AddListener(() => SwatchClicked?.Invoke(idx));

                // Selection outline (behind the fill, slightly larger)
                var outlineGo = new GameObject("Outline", typeof(RectTransform), typeof(Image));
                outlineGo.transform.SetParent(go.transform, false);
                var ort = (RectTransform)outlineGo.transform;
                ort.anchorMin = Vector2.zero;
                ort.anchorMax = Vector2.one;
                ort.offsetMin = new Vector2(-3, -3);
                ort.offsetMax = new Vector2(3, 3);
                outlineGo.transform.SetAsFirstSibling();
                var outline = outlineGo.GetComponent<Image>();
                outline.color = Color.white;
                outline.enabled = false;

                // Price label (overlay, shown only when locked)
                var priceGo = new GameObject("Price", typeof(RectTransform));
                priceGo.transform.SetParent(go.transform, false);
                var prt = (RectTransform)priceGo.transform;
                prt.anchorMin = Vector2.zero;
                prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero;
                prt.offsetMax = Vector2.zero;
                var price = priceGo.AddComponent<TextMeshProUGUI>();
                price.alignment = TextAlignmentOptions.Center;
                price.fontSize = 14;
                price.fontStyle = FontStyles.Bold;
                price.color = Color.white;
                price.text = string.Empty;

                _swatches.Add(new Swatch { BaseColor = dye.Color, Fill = fill, Outline = outline, Price = price });
            }
        }

        public void SetSwatchState(int index, bool unlocked, bool selected, int cost)
        {
            if (index < 0 || index >= _swatches.Count) return;
            var s = _swatches[index];

            var c = s.BaseColor;
            c.a = unlocked ? 1f : 0.4f; // dim locked swatches
            s.Fill.color = c;

            s.Outline.enabled = selected;
            s.Price.text = unlocked ? string.Empty : cost.ToString();
        }
    }
}
