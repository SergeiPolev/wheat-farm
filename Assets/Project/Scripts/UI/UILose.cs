using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

public class UILose : WindowBase
{
    public event Action<UILose> OnHubE;
    public override WindowId WindowID => WindowId.Lose;

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

    public void OnHub()
    {
        OnHubE?.Invoke(this);
    }
}
