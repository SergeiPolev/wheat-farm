using StateMachine;

public interface IStateChanger
{
    public void Enter<TState>() where TState : class, IState;
    public void Enter<TState, TPayload>(TPayload payload) where TState : class, IPayloadedState<TPayload>;
}