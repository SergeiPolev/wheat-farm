using System;
using UnityEngine;

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