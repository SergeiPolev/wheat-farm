using System;

public class WindowModel
{
    public Action OnCloseAction;

    public WindowModel(Action onClosedAction = null)
    {
        OnCloseAction = onClosedAction;
    }
}
