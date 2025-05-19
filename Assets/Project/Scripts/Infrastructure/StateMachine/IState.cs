namespace StateMachine
{
    public interface IState: IExitableState
    {
        void Enter();
    }

    public interface IPayloadedState<TPayload>: IExitableState
    {
        void Enter(TPayload payload);
    }

    public interface IPayloadedState<TPayload1, TPayload2> : IExitableState
    {
        void Enter(TPayload1 payload1, TPayload2 payload2);
    }

    public interface IExitableState
    {
        void Exit();
    }
}