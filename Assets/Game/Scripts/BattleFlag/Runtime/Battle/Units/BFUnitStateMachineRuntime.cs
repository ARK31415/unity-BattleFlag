using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位正式逻辑状态机组件。
    ///
    /// 职责边界：
    /// - 管理 Idle、Move、Attack、Dead 等正式逻辑状态的进入、更新和退出。
    /// - 提供状态对象给移动、攻击和死亡流程协作。
    /// - 不保存 HP/AP、阵营、格子占用或 Hurt 表现状态；Hurt 仅由表现层 Animator 处理。
    /// </summary>
    [DisallowMultipleComponent]
    public class BFUnitStateMachineRuntime : MonoBehaviour, IUnitStateMachine
    {
        private UnitRuntime _owner;
        private UnitIdleState _idleState;
        private UnitMoveState _moveState;
        private UnitAttackState _attackState;
        private UnitDeadState _deadState;

        /// <summary>当前正式逻辑状态。</summary>
        public BaseUnitState CurrentState { get; private set; }

        /// <summary>待机状态，作为初始化和动作完成后的默认状态。</summary>
        public UnitIdleState IdleState => _idleState;

        /// <summary>移动状态，由 UnitManager 在路径移动开始前设置目标格。</summary>
        public UnitMoveState MoveState => _moveState;

        /// <summary>攻击状态，由 UnitManager 在攻击上下文记录成功后设置目标。</summary>
        public UnitAttackState AttackState => _attackState;

        /// <summary>死亡状态，进入后逻辑死亡立即生效，视觉清理等待动画事件。</summary>
        public UnitDeadState DeadState => _deadState;

        /// <summary>
        /// 初始化状态机和状态对象。
        ///
        /// 同一 owner 重复初始化会复用现有状态，避免场景根和 Awake 重复调用时重置运行中状态。
        /// </summary>
        /// <param name="owner">状态机所属的单位根。</param>
        public void Initialize(UnitRuntime owner)
        {
            if (owner == null) return;
            if (_owner == owner && _idleState != null) return;

            _owner = owner;
            _idleState = new UnitIdleState();
            _moveState = new UnitMoveState();
            _attackState = new UnitAttackState();
            _deadState = new UnitDeadState();

            _idleState.Initialize(_owner, this);
            _moveState.Initialize(_owner, this);
            _attackState.Initialize(_owner, this);
            _deadState.Initialize(_owner, this);

            ChangeState(_idleState);
        }

        /// <summary>驱动当前状态的逐帧逻辑更新。</summary>
        public void LogicUpdate()
        {
            CurrentState?.LogicUpdate();
        }

        /// <summary>驱动当前状态的固定步长更新。</summary>
        public void PhysicsUpdate()
        {
            CurrentState?.PhysicsUpdate();
        }

        /// <summary>
        /// 切换正式逻辑状态。
        ///
        /// 空状态和切回当前状态会被忽略，避免重复触发 OnExit/OnEnter。
        /// </summary>
        /// <param name="newState">要进入的目标状态。</param>
        public void ChangeState(BaseUnitState newState)
        {
            if (newState == null || CurrentState == newState) return;

            CurrentState?.OnExit();
            CurrentState = newState;
            CurrentState?.OnEnter();
        }
    }
}
