using Services;
using System;
using UnityEngine;

public abstract class WindowBase : MonoBehaviour
{
    public abstract WindowId WindowID { get; }
    public event Action<WindowBase> OnOpenE, OnCloseE;

    public virtual bool IsOpen => gameObject.activeSelf;

    public void Initialize(AllServices services)
    {
        _Initialize(services);
    }
    protected virtual void _Initialize(AllServices services) { }

    public void Open()
    {
        if (IsOpen)
            return;
        _Open();
        OnOpenE?.Invoke(this);
    }
    protected virtual void _Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        if (IsOpen == false)
            return;
        _Close();
        OnCloseE?.Invoke(this);
    }
    protected virtual void _Close()
    {
        gameObject.SetActive(false);
    }
}