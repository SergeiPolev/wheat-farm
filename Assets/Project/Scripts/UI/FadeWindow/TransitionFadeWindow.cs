using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

public class FadeWindowModel : WindowModel
{
    public float FadeInDelay;
    public float FadeInDuration;
    public float FadeOutDelay;
    public float FadeOutDuration;
    public Action OnFadeInAction;
    public Color Color;

    public FadeWindowModel(float fadeInDelay, float fadeInDuration, float fadeOutDelay, float fadeOutDuration, Action onFadeInAction)
    {
        FadeInDelay = fadeInDelay;
        FadeInDuration = fadeInDuration;
        FadeOutDelay = fadeOutDelay;
        FadeOutDuration = fadeOutDuration;
        OnFadeInAction = onFadeInAction;
        Color = Color.black;
        Color.a = 0;
    }

    public FadeWindowModel(float fadeInDelay, float fadeInDuration, float fadeOutDelay, float fadeOutDuration, Action onFadeInAction, Color color)
    {
        FadeInDelay = fadeInDelay;
        FadeInDuration = fadeInDuration;
        FadeOutDelay = fadeOutDelay;
        FadeOutDuration = fadeOutDuration;
        OnFadeInAction = onFadeInAction;
        Color = color;
        Color.a = 0;
    }

    public FadeWindowModel(float fadeInDelay, float fadeInDuration, float fadeOutDelay, float fadeOutDuration, Action onFadeInAction, Color color,
        Action onCloseAction): base(onCloseAction)
    {
        FadeInDelay = fadeInDelay;
        FadeInDuration = fadeInDuration;
        FadeOutDelay = fadeOutDelay;
        FadeOutDuration = fadeOutDuration;
        OnFadeInAction = onFadeInAction;
        Color = color;
        Color.a = 0;
    }
}

public class TransitionFadeWindow : WindowBase
{
    public override WindowId WindowID => WindowId.TransitionFade;

    [SerializeField] private Image _background;
    private Sequence _fadeSequence;
    private FadeWindowModel _fadeWindowModel;

    protected override void _Open()
    {
        base._Open();
        RefreshWindow();
    }

    protected void RefreshWindow()
    {
        Color color = _background.color;
        color.a = 0;
        _background.color = color;
    }

    public void SetModel(WindowModel windowModel)
    {
        //base.SetModel(windowModel);

        if (windowModel is FadeWindowModel fadeModel)
        {
            _fadeWindowModel = fadeModel;
            _background.color = fadeModel.Color;
            _fadeSequence = DOTween.Sequence();
            _fadeSequence.Append(_background.DOFade(1f, fadeModel.FadeInDuration).SetDelay(fadeModel.FadeInDelay).OnComplete(OnFadeIn));
            _fadeSequence.Append(_background.DOFade(0f, fadeModel.FadeOutDuration).SetDelay(fadeModel.FadeOutDelay));
            _fadeSequence.SetUpdate(true);
            _fadeSequence.OnComplete(Close);
        }
        else
        {
            throw new System.Exception("Dont find FadeWindowModel");
        }
    }

    private void OnFadeIn()
    {
        _fadeWindowModel?.OnFadeInAction?.Invoke();
    }
}
