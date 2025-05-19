using DG.Tweening;
using UnityEngine;

public class RotateTweenAnimation : MonoBehaviour
{
    [SerializeField] private float _duration = 1f;
    private Tween _rotateTween;

    private void OnEnable()
    {
        StartRotate();
    }

    private void OnDisable()
    {
        StopRotate();
    }

    private void StartRotate()
    {
        transform.rotation = Quaternion.identity;
        _rotateTween = transform.DORotate(new Vector3(0, 0, 360), _duration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1)
            .SetUpdate(true);
    }

    private void StopRotate()
    {
        if (_rotateTween != null)
        {
            _rotateTween.Kill();
        }
    }
}
