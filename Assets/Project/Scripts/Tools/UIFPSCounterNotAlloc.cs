using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CastleDefence
{
    public class UIFPSCounterNotAlloc : MonoBehaviour
    {
        [SerializeField] private Gradient gradient;
        [SerializeField] private float _topFrameRate = 60f;
        [SerializeField] private TMP_Text _average, _min, _max;
        private string[] _frames, _framesMin, _framesMax;

        private void Awake()
        {
            _frames = new string[999];
            for (int i = 0; i < _frames.Length; i++)
                _frames[i] = "FPS:" + i.ToString();

            _framesMin = new string[999];
            for (int i = 0; i < _frames.Length; i++)
                _framesMin[i] = "MIN:" + i.ToString();

            _framesMax = new string[999];
            for (int i = 0; i < _frames.Length; i++)
                _framesMax[i] = "MAX:" + i.ToString();

            FPSCounter.OnUpdateFPS += FPSCounter_OnUpdateFPS;
        }

        private void OnDestroy()
        {
            FPSCounter.OnUpdateFPS -= FPSCounter_OnUpdateFPS;
        }

        private void FPSCounter_OnUpdateFPS()
        {
            var avg = Mathf.Clamp(Mathf.RoundToInt(FPSCounter.AverageFPS), 0, _frames.Length - 1);
            var min = Mathf.Clamp(Mathf.RoundToInt(FPSCounter.MinFPS), 0, _framesMin.Length - 1);
            var max = Mathf.Clamp(Mathf.RoundToInt(FPSCounter.MaxFPS), 0, _framesMax.Length - 1);

            _average.text = _frames[avg];
            _average.color = gradient.Evaluate(FPSCounter.AverageFPS / _topFrameRate);
            _min.text = _framesMin[min];
            _min.color = gradient.Evaluate(FPSCounter.MinFPS / _topFrameRate);
            _max.text = _framesMax[max];
            _max.color = gradient.Evaluate(FPSCounter.MaxFPS / _topFrameRate);
        }
    }
}
