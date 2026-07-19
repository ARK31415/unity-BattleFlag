using System;
using BF.Game.Runtime.Battle.Commands;
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

        private UnitRuntime _queuedAttackTarget;
        private bool _hasQueuedAttack;
        private bool _hasResolvedQueuedAttack;

        public event Action<UnitRuntime> HurtReceived;
        public event Action<UnitRuntime> DeathStarted;

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
            ApplyDamage(damage);
        }

        /// <summary>
        /// 应用结算后的最终伤害（由结算层调用）。
        /// </summary>
        public void ApplyResolvedDamage(int finalDamage)
        {
            ApplyDamage(finalDamage);
        }

        private void ApplyDamage(int damage)
        {
            if (!IsAlive) return;

            int appliedDamage = Mathf.Max(0, damage);
            if (appliedDamage <= 0) return;

            CurrentHP = Mathf.Max(0, CurrentHP - appliedDamage);

            if (IsAlive)
            {
                HurtReceived?.Invoke(this);
            }
            else
            {
                DeathStarted?.Invoke(this);
                ChangeState(_deadState);
            }
        }

        /// <summary>
        /// 是否有待结算的攻击。
        /// </summary>
        public bool HasQueuedAttack => _hasQueuedAttack && !_hasResolvedQueuedAttack;

        /// <summary>
        /// 开始待结算攻击（由 BFBattleUnitManager 调用）。
        /// </summary>
        public bool BeginQueuedAttack(UnitRuntime target)
        {
            if (target == null || !target.IsAlive)
            {
                Debug.LogWarning($"[UnitRuntime] {DisplayName} 无法攻击空目标或已死亡目标。");
                return false;
            }

            if (!IsAlive)
            {
                Debug.LogWarning($"[UnitRuntime] {DisplayName} 已死亡，无法发起攻击。");
                return false;
            }

            if (_hasQueuedAttack && !_hasResolvedQueuedAttack)
            {
                Debug.LogWarning($"[UnitRuntime] {DisplayName} 已有待结算攻击。");
                return false;
            }

            _queuedAttackTarget = target;
            _hasQueuedAttack = true;
            _hasResolvedQueuedAttack = false;

            _attackState.SetTarget(target);
            ChangeState(_attackState);

            Debug.Log($"[UnitRuntime] {DisplayName} 开始待结算攻击 -> {target.DisplayName}");
            return true;
        }

        /// <summary>
        /// 尝试消费待结算攻击上下文（由动画命中帧事件调用）。
        /// </summary>
        public bool TryConsumeQueuedAttack(out BFAttackContext context)
        {
            context = default;

            if (!_hasQueuedAttack || _hasResolvedQueuedAttack)
            {
                return false;
            }

            if (_queuedAttackTarget == null || !_queuedAttackTarget.IsAlive)
            {
                Debug.LogWarning($"[UnitRuntime] {DisplayName} 的攻击目标无效。");
                _hasQueuedAttack = false;
                _queuedAttackTarget = null;
                return false;
            }

            context = new BFAttackContext(this, _queuedAttackTarget);
            _hasResolvedQueuedAttack = true;

            return true;
        }

        /// <summary>
        /// 通知攻击已结算完成（清理攻击状态）。
        /// </summary>
        public void NotifyAttackResolved()
        {
            _hasQueuedAttack = false;
            _hasResolvedQueuedAttack = false;
            _queuedAttackTarget = null;

            if (IsAlive)
            {
                ChangeState(_idleState);
            }
        }

        /// <summary>
        /// 通知死亡视觉动画完成，执行最终清理。
        /// </summary>
        public void FinalizeDeathVisualCleanup()
        {
            if (CurrentState != _deadState) return;

            if (gameObject != null)
            {
                var spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer != null) spriteRenderer.enabled = false;

                var animator = GetComponent<Animator>();
                if (animator != null) animator.enabled = false;

                var collider = GetComponent<Collider2D>();
                if (collider != null) collider.enabled = false;

                gameObject.SetActive(false);
            }

            Debug.Log($"[UnitRuntime] {DisplayName} 死亡视觉清理完成。");
        }

        public UnitMoveState GetMoveState() => _moveState;
        public UnitAttackState GetAttackState() => _attackState;
        public UnitIdleState GetIdleState() => _idleState;
    }
}
