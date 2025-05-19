using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Services
{
    public class PauseService : IService
    {
        private List<object> _objects = new List<object>();
        protected float _defaultTimeScale = 1f;
        private Tween _timeScaleTween;
        public bool IsPaused { get; private set; }

        public void Pause(object source, float delay)
        {
            _objects.Add(source);
            float timeScale = Time.timeScale;
            _timeScaleTween?.Kill();

            if (timeScale != 0f)
            {
                Tween tween = DOTween.To(() => timeScale, x => timeScale = x, 0f, delay);
                tween.SetUpdate(true);
                tween.OnUpdate(() => SetTimeScale(timeScale));
                tween.OnComplete(() => SetTimeScale(0f));
            }
        }

        public void Pause(object source)
        {
            _objects.Add(source);

            if (_objects.Count == 1)
            {
                SetTimeScale(0f);
            }
        }

        public void Unpause(object source)
        {
            _objects.Remove(source);

            if (_objects.Count == 0)
            {
                SetTimeScale(_defaultTimeScale);
            }
        }

        public void Unpause(object source, float delay)
        {
            _objects.Remove(source);
            _timeScaleTween?.Kill();

            if (_objects.Count == 0)
            {
                float timeScale = 0f;
                Tween tween = DOTween.To(() => timeScale, x => timeScale = x, 1f, delay);
                tween.SetUpdate(true);
                tween.OnUpdate(() => SetTimeScale(timeScale));
                tween.OnComplete(() => SetTimeScale(1f));
            }
        }

        public void SetTimeScale(float value)
        {
            IsPaused = value != 1f;
            Time.timeScale = value;
        }
    }
}

