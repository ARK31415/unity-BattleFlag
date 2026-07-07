using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位状态基类。
    /// 采用四段式生命周期：OnEnter / LogicUpdate / PhysicsUpdate / OnExit。
    /// 基类禁止引用具体子类名与动画名。
    /// </summary>
    public abstract class BaseUnitState
    {
        /// <summary>
        /// 状态所属的单位运行时。
        /// </summary>
        protected UnitRuntime Owner { get; private set; }

        /// <summary>
        /// 状态机引用（用于状态切换）。
        /// </summary>
        protected IUnitStateMachine StateMachine { get; private set; }

        /// <summary>
        /// 初始化状态（在状态首次切换到此状态时调用）。
        /// </summary>
        public virtual void Initialize(UnitRuntime owner, IUnitStateMachine stateMachine)
        {
            Owner = owner;
            StateMachine = stateMachine;
        }

        /// <summary>
        /// 进入状态时调用一次。
        /// </summary>
        public abstract void OnEnter();

        /// <summary>
        /// 逻辑更新，每帧调用（依赖帧率）。
        /// </summary>
        public abstract void LogicUpdate();

        /// <summary>
        /// 物理更新，固定时间步长调用。
        /// </summary>
        public abstract void PhysicsUpdate();

        /// <summary>
        /// 退出状态时调用一次。
        /// </summary>
        public abstract void OnExit();

        /// <summary>
        /// 状态名称（调试用），子类应覆写。
        /// </summary>
        public virtual string StateName => GetType().Name;
    }

    /// <summary>
    /// 单位状态机最小接口。
    /// 由 UnitRuntime 实现，供 BaseUnitState 回调使用。
    /// </summary>
    public interface IUnitStateMachine
    {
        /// <summary>
        /// 切换到指定状态。
        /// </summary>
        void ChangeState(BaseUnitState newState);

        /// <summary>
        /// 当前活跃状态。
        /// </summary>
        BaseUnitState CurrentState { get; }
    }
}
