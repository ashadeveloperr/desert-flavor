namespace Jay.FSM
{
    public interface IState
    {
        void Enter();
        void Exit();
    }

    public interface IUpdatableState : IState
    {
        void Tick(float deltaTime);
    }
}
