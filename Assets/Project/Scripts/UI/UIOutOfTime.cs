using DG.Tweening;
using System;
using UnityEngine;

public class UIOutOfTime : WindowBase
{
    public event Action<UIOutOfTime> OnTryAgainE;
    public override WindowId WindowID => WindowId.OutOfTime;

    [SerializeField] private CanvasGroup _group;

    protected override void _Open()
    {
        base._Open();
        _group.alpha = 0;
        _group.DOFade(1, 0.5f);
    }

    protected override void _Close()
    {
        DOTween.Sequence(this)
            .Append(_group.DOFade(0, 0.5f))
            .AppendCallback(() =>
            {
                base._Close();
            });
    }

    public void OnTryAgain()
    {
        OnTryAgainE?.Invoke(this);
    }
}
