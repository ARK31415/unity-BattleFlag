using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位表现状态机组件。
    ///
    /// 规则层的 BFUnit_ActionState 与本状态机保持独立；本组件只管理 Idle、Move、Attack、Dead
    /// 等表现阶段，以及表现层所需的目标和动画协作数据。
    /// </summary>
    [DisallowMultipleComponent]
    public class BFUnit_PresentationStateMachineRuntime : MonoBehaviour, IBFUnit_PresentationStateMachine
    {
        private UnitRuntime _owner;
        private BFUnit_PresentationIdleState _idleState;
        private BFUnit_PresentationMoveState _moveState;
        private BFUnit_PresentationAttackState _attackState;
        private BFUnit_PresentationDeadState _deadState;

        /// <summary>当前表现状态。</summary>
        public BFUnit_PresentationState CurrentState { get; private set; }

        /// <summary>表现待机状态。</summary>
        public BFUnit_PresentationIdleState IdleState => _idleState;

        /// <summary>表现移动状态。</summary>
        public BFUnit_PresentationMoveState MoveState => _moveState;

        /// <summary>表现攻击状态。</summary>
        public BFUnit_PresentationAttackState AttackState => _attackState;

        /// <summary>表现阵亡状态。</summary>
        public BFUnit_PresentationDeadState DeadState => _deadState;

        /// <summary>
        /// 初始化表现状态机和状态对象。
        /// 同一单位重复初始化会复用现有状态，避免生命周期回调重置运行中的表现阶段。
        /// </summary>
        public void Initialize(UnitRuntime owner)
        {
            if (owner == null) return;
            if (_owner == owner && _idleState != null) return;

            _owner = owner;
            _idleState = new BFUnit_PresentationIdleState();
            _moveState = new BFUnit_PresentationMoveState();
            _attackState = new BFUnit_PresentationAttackState();
            _deadState = new BFUnit_PresentationDeadState();

            _idleState.Initialize(_owner, this);
            _moveState.Initialize(_owner, this);
            _attackState.Initialize(_owner, this);
            _deadState.Initialize(_owner, this);

            ChangeState(_idleState);
        }

        /// <summary>驱动当前表现状态的逐帧逻辑更新。</summary>
        public void LogicUpdate()
        {
            CurrentState?.LogicUpdate();
        }

        /// <summary>驱动当前表现状态的固定步长更新。</summary>
        public void PhysicsUpdate()
        {
            CurrentState?.PhysicsUpdate();
        }

        /// <summary>
        /// 切换表现状态；空状态和切回当前状态会被忽略。
        /// </summary>
        public void ChangeState(BFUnit_PresentationState newState)
        {
            if (newState == null || CurrentState == newState) return;

            CurrentState?.OnExit();
            CurrentState = newState;
            CurrentState.OnEnter();
        }
    }
}
