using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheatFarm.UI
{
    /// <summary>
    /// HUD view — always visible. Shows coins, active tool, time of day.
    /// Pure visual: no logic, no subscriptions. Driven by HUDPresenter.
    /// </summary>
    public class HUDView : MonoBehaviour
    {
        [Header("Economy")]
        [SerializeField] private TextMeshProUGUI _coinsText;

        [Header("Tools")]
        [SerializeField] private Image[] _toolIcons;
        [SerializeField] private Color _activeToolColor = Color.white;
        [SerializeField] private Color _inactiveToolColor = new(1f, 1f, 1f, 0.4f);

        [Header("Day/Night")]
        [SerializeField] private TextMeshProUGUI _timeText;
        [SerializeField] private Image _timeFill;

        private int _lastHighlighted = -1;

        public void UpdateCoins(int amount)
        {
            if (_coinsText != null)
                _coinsText.text = amount.ToString();
        }

        public void HighlightTool(int index)
        {
            if (_toolIcons == null || _toolIcons.Length == 0) return;

            // Unhighlight previous
            if (_lastHighlighted >= 0 && _lastHighlighted < _toolIcons.Length && _toolIcons[_lastHighlighted] != null)
                _toolIcons[_lastHighlighted].color = _inactiveToolColor;

            // Highlight new
            if (index >= 0 && index < _toolIcons.Length && _toolIcons[index] != null)
                _toolIcons[index].color = _activeToolColor;

            _lastHighlighted = index;
        }

        public void UpdateTime(string phaseName)
        {
            if (_timeText != null)
                _timeText.text = phaseName;
        }

        public void UpdateTimeFill(float normalized)
        {
            if (_timeFill != null)
                _timeFill.fillAmount = normalized;
        }
    }
}
