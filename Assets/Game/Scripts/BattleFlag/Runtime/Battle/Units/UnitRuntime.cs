using System;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位运行时对象。行动点制：每移动一格消耗 1 点，攻击消耗 1 点。
    /// </summary>
    public class UnitRuntime : MonoBehaviour, IUnitStateMachine
    {
        public string UnitId => gameObject != null ? gameObject.name : "Unknown";

        public string DisplayName = "Unit";

        [field: SerializeField] public UnitFaction Faction { get; set; } = UnitFaction.Player;
        [field: SerializeField] public int MaxHP { get; set; } = 20;
        public int CurrentHP { get; set; } = 20;
        [field: SerializeField] public int Attack { get; set; } = 5;
        [field: SerializeField] public int AttackRange { get; set; } = 1;

        /// <summary>单位职业（战士/法师）。</summary>
        [field: SerializeField] public BFUnitRole Role { get; set; } = BFUnitRole.Warrior;

        /// <summary>攻击消耗的行动点数（战士 2、法师 3，Spec 第 5 节）。</summary>
        [field: SerializeField] public int AttackCost { get; set; } = 2;

        /// <summary>每回合最大行动点数。</summary>
        [field: SerializeField] public int MaxActionPoints { get; set; } = 5;

        /// <summary>本回合剩余行动点数。</summary>
        public int RemainingActionPoints { get; set; } = 5;

        /// <summary>是否已行动完毕。</summary>
        public bool HasActed => RemainingActionPoints <= 0;
        public bool IsAlive => CurrentHP > 0;

        [field: SerializeField] public Vector2Int GridPosition { get; set; }
        public BaseUnitState CurrentState { get; private set; }
        public IMovementHandler MovementHandler { get; set; }

        private UnitIdleState _idleState;
        private UnitMoveState _moveState;
        private UnitAttackState _attackState;
        private UnitDeadState _deadState;

        private void Awake()
        {
            CurrentHP = MaxHP;
            RemainingActionPoints = MaxActionPoints;

            _idleState = new UnitIdleState(); _idleState.Initialize(this, this);
            _moveState = new UnitMoveState(); _moveState.Initialize(this, this);
            _attackState = new UnitAttackState(); _attackState.Initialize(this, this);
            _deadState = new UnitDeadState(); _deadState.Initialize(this, this);

            ChangeState(_idleState);
        }

        private void Update() { CurrentState?.LogicUpdate(); }
        private void FixedUpdate() { CurrentState?.PhysicsUpdate(); }

        public void ChangeState(BaseUnitState newState)
        {
            if (newState == null || CurrentState == newState) return;
            CurrentState?.OnExit();
            CurrentState = newState;
            CurrentState?.OnEnter();
        }

        /// <summary>重置回合行动点。</summary>
        public void ResetTurnActions()
        {
            RemainingActionPoints = MaxActionPoints;
        }

        /// <summary>消耗行动点。</summary>
        public void ConsumeActionPoints(int amount)
        {
            RemainingActionPoints = Mathf.Max(0, RemainingActionPoints - amount);
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;
            CurrentHP = Mathf.Max(0, CurrentHP - damage);
            if (!IsAlive) ChangeState(_deadState);
        }

        public UnitMoveState GetMoveState() => _moveState;
        public UnitAttackState GetAttackState() => _attackState;
        public UnitIdleState GetIdleState() => _idleState;
    }
}
