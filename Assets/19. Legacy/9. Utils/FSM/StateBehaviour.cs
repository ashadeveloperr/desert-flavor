using UnityEngine;

namespace Jay.FSM
{
    public abstract class StateBehaviour : MonoBehaviour, IState
    {
        public abstract void Enter();
        public abstract void Exit();
    }

    public abstract class StateBehaviour<TContext> : StateBehaviour
    {
        protected TContext Context { get; private set; }

        public virtual void Initialize(TContext context)
        {
            Context = context;
        }
    }
}
