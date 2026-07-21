using System;
using BF.Game.Runtime.Battle.Data;
using UnityEngine;

namespace BF.Game.Runtime.Battle.Units
{
    /// <summary>
    /// 单位根组件，作为场景发现锚点、子组件管理器和生命周期协调入口。
    ///
    /// 职责边界：
    /// - 负责缓存并校验 Identity、Stats、Grid、Combat、StateMachine 五个运行时子组件。
    /// - 负责下发战斗开始、回合开始、回合结束和死亡视觉清理等单位级生命周期。
    /// - 不保存阵营、HP、AP、格子、攻击上下文或正式状态等业务数据；外部系统应进入对应子组件读取。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BFUnitIdentityRuntime))]
    [RequireComponent(typeof(BFUnitStatsRuntime))]
    [RequireComponent(typeof(BFUnitGridRuntime))]
    [RequireComponent(typeof(BFUnitCombatRuntime))]
    [RequireComponent(typeof(BFUnitStateMachineRuntime))]
    public class UnitRuntime : MonoBehaviour
    {
        [Header("Runtime Components")]
        // 这些引用可由 Inspector 预设；为空时运行期会从同一根节点缓存或补齐。
        [SerializeField] private BFUnitIdentityRuntime _identity;
        [SerializeField] private BFUnitStatsRuntime _stats;
        [SerializeField] private BFUnitGridRuntime _grid;
        [SerializeField] private BFUnitCombatRuntime _combat;
        [SerializeField] private BFUnitStateMachineRuntime _stateMachine;

        [Header("Optional Visual Cleanup")]
        // 死亡动画完成后统一关闭的表现组件；为空时只跳过对应清理，不影响逻辑死亡。
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Animator _animator;
        [SerializeField] private Collider2D _collider2D;

        /// <summary>单位身份入口，包含显示名、阵营和角色类型。</summary>
        public BFUnitIdentityRuntime Identity => EnsureIdentity();

        /// <summary>单位数值入口，包含 HP、AP、攻击力和消耗等运行时数值。</summary>
        public BFUnitStatsRuntime Stats => EnsureStats();

        /// <summary>单位格子入口，包含当前格和出生格语义。</summary>
        public BFUnitGridRuntime Grid => EnsureGrid();

        /// <summary>单位攻击上下文入口，负责动画命中帧前后的待结算攻击状态。</summary>
        public BFUnitCombatRuntime Combat => EnsureCombat();

        /// <summary>单位正式逻辑状态机入口，只承载 Idle、Move、Attack、Dead 等逻辑状态。</summary>
        public BFUnitStateMachineRuntime StateMachine => EnsureStateMachine();

        /// <summary>单位根 ID，当前沿用 GameObject 名称作为场景手摆阶段的实例标识。</summary>
        public string UnitId => Identity.UnitId;

        /// <summary>当前单位实例使用的数据定义；场景手摆单位可为空。</summary>
        public BFUnitDefinitionSO Definition { get; private set; }

        /// <summary>移动能力注入入口；当前由棋盘管理器在单位吸附到格子时写入。</summary>
        public IMovementHandler MovementHandler { get; set; }

        /// <summary>受到非致死伤害时广播给表现层，Hurt 只作为动画覆盖效果。</summary>
        public event Action<UnitRuntime> HurtReceived;

        /// <summary>进入逻辑死亡时广播给表现层，视觉清理仍等待死亡动画完成事件。</summary>
        public event Action<UnitRuntime> DeathStarted;

        private void Reset()
        {
            CacheRuntimeComponents(addIfMissing: true);
        }

        // 在 Awake 建立最小可用依赖，避免 Root、Manager 或 Presenter 在 Start 前访问到半初始化单位。
        private void Awake()
        {
            InitializeRuntime();
        }

        // 正式逻辑状态机仍由单位根统一驱动，状态数据本身归 StateMachine 组件。
        private void Update()
        {
            _stateMachine?.LogicUpdate();
        }

        private void FixedUpdate()
        {
            _stateMachine?.PhysicsUpdate();
        }

        /// <summary>
        /// 初始化单位根的运行时依赖。
        ///
        /// 该方法可由场景根节点在战斗初始化阶段重复调用；已存在的子组件会被复用，
        /// 缺失组件会按 RequireComponent 合同补齐并记录清晰错误，避免静默半初始化。
        /// </summary>
        public void InitializeRuntime()
        {
            CacheRuntimeComponents(addIfMissing: true);
            CacheOptionalVisualComponents();
            ValidateRuntimeComponents();
            _stateMachine.Initialize(this);
        }

        /// <summary>
        /// 进入战斗时重置单位战斗资源并回到 Idle。
        ///
        /// 只负责触发生命周期，不直接写 HP/AP 字段；具体数值仍由 Stats 组件维护。
        /// </summary>
        public void BeginBattle()
        {
            InitializeRuntime();
            Stats.ResetBattleResources();
            StateMachine.ChangeState(StateMachine.IdleState);
        }

        /// <summary>
        /// 使用单位定义和生成上下文初始化运行时子组件。
        /// </summary>
        public void InitializeFromDefinition(BFUnitDefinitionSO definition, BFUnitSpawnContext spawnContext)
        {
            if (definition == null)
            {
                Debug.LogError("[UnitRuntime] Cannot initialize from a missing unit definition.", this);
                return;
            }

            if (!definition.ValidateConfiguration(out string error))
            {
                Debug.LogError($"[UnitRuntime] {error}", this);
                return;
            }

            Definition = definition;
            InitializeRuntime();

            Identity.InitializeFromConfig(definition.ImportedConfig, spawnContext.Faction);
            Stats.InitializeBaseStats(definition.GetBaseStats(), resetResources: true);
            Grid.InitializeSpawnPosition(spawnContext.GridPosition);
            ApplyUnityBinding(definition.UnityBinding);
        }

        /// <summary>
        /// 回合开始入口。当前只重置本回合 AP，后续本回合临时状态也应从这里统一下发。
        /// </summary>
        public void BeginTurn()
        {
            Stats.ResetTurnActions();
        }

        /// <summary>
        /// 回合结束入口。当前清理未完成的攻击上下文，避免跨回合残留待结算攻击。
        /// </summary>
        public void EndTurn()
        {
            Combat.ClearQueuedAttack();
        }

        /// <summary>
        /// 对单位施加直接伤害。
        ///
        /// 规则层只关心伤害结果；HP 扣减由 Stats 处理，受伤或死亡事件由单位根统一广播给表现层。
        /// </summary>
        /// <param name="damage">待应用的伤害值；非正数不会触发受伤或死亡事件。</param>
        public void TakeDamage(int damage)
        {
            ApplyDamage(damage);
        }

        /// <summary>
        /// 应用结算层计算后的最终伤害。
        ///
        /// 该入口保留给 BFAttackResolver 调用，避免结算层直接修改 Stats 内部字段。
        /// </summary>
        /// <param name="finalDamage">已经完成公式计算和修正后的最终伤害。</param>
        public void ApplyResolvedDamage(int finalDamage)
        {
            ApplyDamage(finalDamage);
        }

        /// <summary>
        /// 死亡动画完成后的最终视觉清理入口。
        ///
        /// 只有单位已经处于 Dead 正式状态时才会执行，保证逻辑死亡先发生，表现对象延迟隐藏。
        /// </summary>
        public void FinalizeDeathVisualCleanup()
        {
            if (StateMachine.CurrentState != StateMachine.DeadState) return;

            if (gameObject != null)
            {
                if (_spriteRenderer != null) _spriteRenderer.enabled = false;

                if (_animator != null) _animator.enabled = false;

                if (_collider2D != null) _collider2D.enabled = false;

                gameObject.SetActive(false);
            }

            Debug.Log($"[UnitRuntime] {Identity.DisplayName} death visual cleanup finished.");
        }

        private void ApplyDamage(int damage)
        {
            if (!Stats.TryApplyDamage(damage, out bool wasKilled)) return;

            if (wasKilled)
            {
                // 先通知表现层进入死亡动画，再切正式 Dead 状态，保持旧动画合同稳定。
                DeathStarted?.Invoke(this);
                StateMachine.ChangeState(StateMachine.DeadState);
                return;
            }

            HurtReceived?.Invoke(this);
        }

        private BFUnitIdentityRuntime EnsureIdentity()
        {
            if (_identity == null) CacheRuntimeComponents(addIfMissing: true);
            return _identity;
        }

        private BFUnitStatsRuntime EnsureStats()
        {
            if (_stats == null) CacheRuntimeComponents(addIfMissing: true);
            return _stats;
        }

        private BFUnitGridRuntime EnsureGrid()
        {
            if (_grid == null) CacheRuntimeComponents(addIfMissing: true);
            return _grid;
        }

        private BFUnitCombatRuntime EnsureCombat()
        {
            if (_combat == null) CacheRuntimeComponents(addIfMissing: true);
            return _combat;
        }

        private BFUnitStateMachineRuntime EnsureStateMachine()
        {
            if (_stateMachine == null) CacheRuntimeComponents(addIfMissing: true);
            _stateMachine.Initialize(this);
            return _stateMachine;
        }

        private void CacheRuntimeComponents(bool addIfMissing)
        {
            _identity = GetOrAddComponent(_identity, addIfMissing);
            _stats = GetOrAddComponent(_stats, addIfMissing);
            _grid = GetOrAddComponent(_grid, addIfMissing);
            _combat = GetOrAddComponent(_combat, addIfMissing);
            _stateMachine = GetOrAddComponent(_stateMachine, addIfMissing);
        }

        private void CacheOptionalVisualComponents()
        {
            if (_spriteRenderer == null) TryGetComponent(out _spriteRenderer);
            if (_animator == null) TryGetComponent(out _animator);
            if (_collider2D == null) TryGetComponent(out _collider2D);
        }

        private void ValidateRuntimeComponents()
        {
            if (_identity == null) Debug.LogError("[UnitRuntime] Missing BFUnitIdentityRuntime.", this);
            if (_stats == null) Debug.LogError("[UnitRuntime] Missing BFUnitStatsRuntime.", this);
            if (_grid == null) Debug.LogError("[UnitRuntime] Missing BFUnitGridRuntime.", this);
            if (_combat == null) Debug.LogError("[UnitRuntime] Missing BFUnitCombatRuntime.", this);
            if (_stateMachine == null) Debug.LogError("[UnitRuntime] Missing BFUnitStateMachineRuntime.", this);
        }

        private void ApplyUnityBinding(BFUnitUnityBindingSO binding)
        {
            if (binding == null) return;
            if (_animator == null || binding.AnimatorController == null) return;

            _animator.runtimeAnimatorController = binding.AnimatorController;
        }

        private T GetOrAddComponent<T>(T current, bool addIfMissing) where T : Component
        {
            if (current != null) return current;
            if (TryGetComponent(out T found)) return found;
            return addIfMissing ? gameObject.AddComponent<T>() : null;
        }
    }
}
