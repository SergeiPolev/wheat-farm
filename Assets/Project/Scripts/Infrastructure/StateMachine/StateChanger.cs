using System;

public class StateChanger<T> where T : Enum
{
    public T State { get; protected set; }
    public T PreviousState { get; protected set; }
    public event Action<T> OnStateChanged;

    public void SetState(T state)
    {
        PreviousState = State;
        State = state;
        OnStateChanged?.Invoke(State);
    }
}