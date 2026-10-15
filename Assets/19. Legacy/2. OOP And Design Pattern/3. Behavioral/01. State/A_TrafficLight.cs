using System;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Behavioral
{
    // State - 예시 A : 신효ㅗ등
    // 상황 : 빨강 -> 초록 -> 노랑 -> 빨강 순으로
    // 순환하며 각 색마다 할 일이 달라진다

    public abstract class LightState
    {
        protected TrafficLight Owner { get; private set; }
        public float Duration = 20f;
        protected  LightState(TrafficLight owner)
        {
            Owner = owner;
        }

        public virtual void Enter() { }
        // public abstract void Update();
        // Unity Update와 혼선이 있어서 저는 다른 네이밍을 사용합니다
        public abstract void Tick(float deltaTime);
        public virtual void Exit() { }
    }
    
    public class RedState : LightState
    {
        public RedState(TrafficLight owner) : base(owner) { }

        public override void Enter()
        {
            Duration = 20f;
        }

        public override void Tick(float deltaTime)
        {
            Debug.Log($"빨강 : 정지");
            Duration -= deltaTime;
            if (Duration <= 0f) Owner.ChangeState(Owner.Green);
        }
    }
    
    public class GreenState : LightState
    {
        public GreenState(TrafficLight owner) : base(owner) { }

        public override void Enter()
        {
            Duration = 30f;
        }

        public override void Tick(float deltaTime)
        {
            Debug.Log($"초록 : 통행");
            Duration -= deltaTime;
            if (Duration <= 0f) Owner.ChangeState(Owner.Yellow);
        }
    }
    
    public class YellowState : LightState
    {
        public YellowState(TrafficLight owner) : base(owner) { }

        public override void Enter()
        {
            Duration = 5;
        }
        
        public override void Tick(float deltaTime)
        {
            Debug.Log($"노랑 : 곧 정지");
            Duration -= deltaTime;
            if (Duration <= 0f) Owner.ChangeState(Owner.Red);
        }
    }


    public class TrafficLight : MonoBehaviour
    {
        public LightState Red { get; private set; }
        public LightState Green { get; private set; }
        public LightState Yellow { get; private set; }

        private LightState CurrentState { get; set; }

        public void ChangeState(LightState next)
        {
            CurrentState?.Exit();
            CurrentState = next;
            CurrentState.Enter();
        }

        private void Awake()
        {
            Red = new RedState(this);
            Green = new GreenWithLeftState(this);
            Yellow = new YellowState(this);
        }

        private void Update()
        {
            CurrentState?.Tick(Time.deltaTime);
        }
    }
    
    // --------------------

    public class GreenWithLeftState : GreenState
    {
        public GreenWithLeftState(TrafficLight owner) : base(owner) { }

        public override void Tick(float deltaTime)
        {
            if(Duration > 10f) Debug.Log($"초록 : 좌회전 통행");
            base.Tick(deltaTime);
        }
    }
}